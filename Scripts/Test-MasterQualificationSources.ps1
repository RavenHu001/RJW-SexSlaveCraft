#requires -Version 7.0
<#
.SYNOPSIS
用本机 RimWorld 原版补丁类核对神器参照投放，并验证模块不进入商人出售库存。
.DESCRIPTION
在独立 pwsh 进程运行，只读游戏文件；在内存里构造原版 PatchOperation 并执行 Apply。
反射填充补丁字段，不调用完整游戏 XML 反序列化、Def 数据库、交易库存/奖励随机生成或存档。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [ValidateSet('1.6', '1.7')][string]$GameVersion = '1.7',
    [string]$RuntimeRoot,
    [string]$GameAssemblyPath,
    [string]$ReportPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if (-not $RuntimeRoot) { $RuntimeRoot = Join-Path $repo $GameVersion }
$runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path
$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path
if (!$ReportPath) { $ReportPath = Join-Path $repo '.builds/master-sources-3b/native-source-audit.json' }
$reportFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportPath)
if (-not $GameAssemblyPath) {
    $GameAssemblyPath = Join-Path $gameRootPath $(if ($GameVersion -eq '1.7') { 'Assembly-CSharp.dll' } else { 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll' })
}
$gameDll = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($GameAssemblyPath)
$patchPath = Join-Path $runtime 'Patches/SSC_MasterQualificationSources.xml'
$itemPath = Join-Path $runtime 'Defs/ThingsDefs/SSC_MasterQualificationItem.xml'
$coreLootPath = Join-Path $gameRootPath 'Data/Core/Defs/ThingSetMakerDefs/ThingSetMakers_MapGen.xml'
$betterLootPath = Join-Path $gameRootPath 'Data/Ideology/Defs/ThingSetMakerDefs/ThingSetMakers_MapGen.xml'
$coreRewardPath = Join-Path $gameRootPath 'Data/Core/Defs/ThingSetMakerDefs/ThingSetMakers_Reward.xml'
$report = [ordered]@{
    StartedAtUtc = [DateTime]::UtcNow.ToString('o'); Success = $false
    GameVersion = $GameVersion; RuntimeRoot = $runtime
    Scope = 'Real PatchOperation.Apply on native Core/Ideology XML; native trade exclusion and artifact reward tag connection. Not full game XML deserialization, Def loading, reward rolls or gameplay.'
    Inputs = [ordered]@{}; Checks = [Collections.Generic.List[string]]::new(); Error = $null
}
function Require([bool]$condition, [string]$message) { if (!$condition) { throw $message } }
function Record([string]$name) { $report.Checks.Add($name); Write-Output "PASS $name" }
function Read-Xml([string]$path) {
    $report.Inputs[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    [xml](Get-Content -LiteralPath $path -Raw -Encoding utf8)
}
try {
    $report.Inputs[$gameDll] = (Get-FileHash -LiteralPath $gameDll -Algorithm SHA256).Hash
    $game = [Reflection.Assembly]::LoadFrom($gameDll)
    # 本机 Apply 的分析器访问 Prefs.LogVerbose；独立进程提供空的内存设置，绝不调用 Init/Save。
    # 不运行 PrefsData 构造器以免触发 Unity 显示设备查询，此审计仅需要默认 false 的日志开关。
    $prefsField = $game.GetType('Verse.Prefs', $true).GetField('data', [Reflection.BindingFlags]'Static,NonPublic')
    $prefsField.SetValue($null, [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($prefsField.FieldType))
    $patch = Read-Xml $patchPath
    $itemDoc = Read-Xml $itemPath
    $coreLoot = Read-Xml $coreLootPath
    $reward = Read-Xml $coreRewardPath
    $betterLoot = if (Test-Path -LiteralPath $betterLootPath) { Read-Xml $betterLootPath } else { $null }
    $moduleName = 'SSC_MasterQualificationItem'
    $item = $itemDoc.SelectSingleNode('/Defs/ThingDef[defName="SSC_MasterQualificationItem"]')

    # 只构造本补丁需要的三个原版类型，逐个字段填充，避免初始化整套 Def/图像/游戏状态。
    function Native-Patch($node) {
        $typeName = 'Verse.' + $node.GetAttribute('Class')
        Require ($typeName -in @('Verse.PatchOperationSequence','Verse.PatchOperationAdd','Verse.PatchOperationConditional')) 'Unexpected patch type'
        $type = $game.GetType($typeName, $true)
        $instance = [Activator]::CreateInstance($type)
        $flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
        if ($typeName -eq 'Verse.PatchOperationSequence') {
            $field = $type.GetField('operations', $flags)
            $list = [Activator]::CreateInstance($field.FieldType)
            foreach ($child in $node.SelectNodes('operations/li')) { [void]$list.Add((Native-Patch $child)) }
            $field.SetValue($instance, $list)
        } else {
            $type.GetField('xpath', $flags).SetValue($instance, [string]$node.SelectSingleNode('xpath').InnerText)
            if ($typeName -eq 'Verse.PatchOperationAdd') {
                $field = $type.GetField('value', $flags)
                $container = [Activator]::CreateInstance($field.FieldType)
                $field.FieldType.GetField('node').SetValue($container, $node.SelectSingleNode('value'))
                $field.SetValue($instance, $container)
            } else {
                $type.GetField('match', $flags).SetValue($instance, (Native-Patch $node.SelectSingleNode('match')))
            }
        }
        return $instance
    }
    $nativePatch = Native-Patch $patch.SelectSingleNode('/Patch/Operation')
    Record 'Native patch constructors and exact field types'

    # Core-only 与含 Ideology 两种输入分别执行真正的 Apply，不使用测试夹具或另写 XML 补丁器。
    foreach ($includeBetter in @($false,$true)) {
        if ($includeBetter -and $null -eq $betterLoot) { continue }
        $defs = [xml]'<Defs/>'
        foreach ($node in $coreLoot.SelectNodes('/Defs/ThingSetMakerDef')) {
            [void]$defs.DocumentElement.AppendChild($defs.ImportNode($node, $true))
        }
        if ($includeBetter) {
            foreach ($optionalName in @('MapGen_AncientComplexRoomLoot_Better','MapGen_AncientComplex_SecurityCrate')) {
                $node = $betterLoot.SelectSingleNode("/Defs/ThingSetMakerDef[defName='$optionalName']")
                Require ($null -ne $node) "Ideology loot definition missing: $optionalName"
                [void]$defs.DocumentElement.AppendChild($defs.ImportNode($node, $true))
            }
        }
        $before = @{}
        foreach ($node in $defs.SelectNodes('/Defs/ThingSetMakerDef')) { $before[[string]$node.defName] = $node.OuterXml }
        Require ($nativePatch.Apply($defs)) "Native Apply failed (Ideology=$includeBetter)"
        $targets = @('MapGen_AncientTempleContents','MapGen_AncientComplexRoomLoot_Default')
        if ($includeBetter) { $targets += @('MapGen_AncientComplexRoomLoot_Better','MapGen_AncientComplex_SecurityCrate') }
        foreach ($name in $targets) {
            $node = $defs.SelectSingleNode("/Defs/ThingSetMakerDef[defName='$name']")
            $modules = $node.SelectNodes(".//thingDefs/li[text()='$moduleName']")
            Require ($modules.Count -eq 1) "Missing or duplicated module source: $name"
            $module = $modules[0]
            $group = $module.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode
            if ($name -eq 'MapGen_AncientComplexRoomLoot_Default') {
                Require ($group.SelectSingleNode('weight').InnerText -eq '0.002') 'Core room module weight is not artifact-equivalent'
                Require ($group.SelectSingleNode('thingSetMaker/fixedParams/countRange').InnerText -eq '1') 'Core room count is not one'
                [void]$group.ParentNode.RemoveChild($group)
            } else {
                # 其他区域直接并入神器组；既有概率/权重/候选不改，避免把整个组概率当作单件概率。
                $referenceName = if ($name -eq 'MapGen_AncientTempleContents') { 'MechSerumResurrector' } else { 'Apparel_PsychicShockLance' }
                Require ($null -ne $module.ParentNode.SelectSingleNode("li[text()='$referenceName']")) "Module not in artifact group: $name"
                [void]$module.ParentNode.RemoveChild($module)
            }
            # 删除模块追加后，整份定义必须与输入相同，包括原始神器组及所有无关选项。
            Require ($node.OuterXml -ceq $before[$name]) "Original loot changed: $name"
            Record "Native append preserved $name (Ideology=$includeBetter)"
        }
        foreach ($node in $defs.SelectNodes('/Defs/ThingSetMakerDef')) {
            if ([string]$node.defName -notin $targets) { Require ($node.OuterXml -ceq $before[[string]$node.defName]) 'Unrelated loot changed' }
        }
        Record "Native patch leaves unrelated loot intact (Ideology=$includeBetter)"
    }

    # 空贸易标签加 Sellable 双重停用商人出售库存，仍可由玩家卖出已取得的模块。
    $thingType = $game.GetType('Verse.ThingDef', $true)
    foreach ($name in @('tradeability','tradeTags','thingSetMakerTags','minRewardCount')) {
        Require ($null -ne $thingType.GetField($name)) "Missing native ThingDef field: $name"
    }
    # ThingDef 构造器会初始化 Unity 图像；这里只提供 HandlesThingDef 实际读取的三个字段。
    $nativeThing = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($thingType)
    $thingType.GetField('defName').SetValue($nativeThing, $moduleName)
    Require ($null -eq $item.SelectSingleNode('tradeTags')) 'Module retained a trader generation tag'
    $tagList = [Collections.Generic.List[string]]::new()
    $thingType.GetField('tradeTags').SetValue($nativeThing, $tagList)
    foreach ($name in @('tradeability','techLevel')) {
        $field = $thingType.GetField($name)
        $field.SetValue($nativeThing, [Enum]::Parse($field.FieldType, [string]$item.SelectSingleNode($name).InnerText))
    }
    $generatorType = $game.GetType('RimWorld.StockGenerator_Tag', $true)
    $nativeGenerator = [Activator]::CreateInstance($generatorType)
    $generatorType.GetField('tradeTag').SetValue($nativeGenerator, 'ExoticMisc')
    Require (!$nativeGenerator.HandlesThingDef($nativeThing)) 'Module is still handled by exotic trader tags'
    $tradeUtility = $game.GetType('RimWorld.TradeabilityUtility', $true)
    $tradeValue = $thingType.GetField('tradeability').GetValue($nativeThing)
    Require (!$tradeUtility.GetMethod('TraderCanSell').Invoke($null, [object[]]@($tradeValue))) 'Generic traders can still sell module'
    Require ($tradeUtility.GetMethod('PlayerCanSell').Invoke($null, [object[]]@($tradeValue))) 'Player sale was unnecessarily disabled'
    Record 'Real trader tag handling and TraderCanSell both exclude module; player sale remains'

    # 用实际心灵枪 Def 核对任务标签，预算仍由原版奖励生成器决定。
    $lanceDoc = Read-Xml (Join-Path $gameRootPath 'Data/Core/Defs/ThingDefs_Misc/Apparel_Utility.xml')
    foreach ($lanceName in @('Apparel_PsychicShockLance','Apparel_PsychicInsanityLance')) {
        Require ($null -ne $lanceDoc.SelectSingleNode("/Defs/ThingDef[defName='$lanceName']/thingSetMakerTags/li[text()='RewardStandardHighFreq']")) "Native lance reward tag mismatch: $lanceName"
    }
    Require ($null -ne $reward.SelectSingleNode('/Defs/ThingSetMakerDef[defName="Reward_ItemsStandard"]//thingSetMakerTagsToAllow/li[text()="RewardStandardHighFreq"]')) 'Core artifact reward tag missing'
    Require ($item.thingSetMakerTags.li -eq 'RewardStandardHighFreq' -and $item.minRewardCount -eq '1') 'Module artifact reward eligibility mismatch'
    Record 'Actual Core standard reward filter matches both psychic lances and module'
    $report.Success = $true
} catch {
    $report.Error = $_.Exception.ToString()
    throw
} finally {
    $report.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportFile)) | Out-Null
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportFile -Encoding utf8
    Write-Output "Report: $reportFile"
}
