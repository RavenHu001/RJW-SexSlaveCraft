#requires -Version 7.0
<#
.SYNOPSIS
核对战斗员发布资源、四语和本机 RimWorld 组件构造。
.DESCRIPTION
在独立 pwsh 进程运行。GameRoot 指向已安装的 RimWorld，不下载或复制游戏程序集。
读取根目录发布 XML 与已编译的模组 DLL；报告包含输入哈希、通过项和失败原因。
只执行独立构造及 AbilityComp.Initialize，不运行完整 XML 加载、施法、战斗或 Scribe。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [string]$ModAssemblyPath,
    [string]$ReportPath
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$gameRootPath = (Resolve-Path -LiteralPath $GameRoot).Path
if (!$ModAssemblyPath) { $ModAssemblyPath = Join-Path $repo 'Assemblies/Sexslavecraft.dll' }
if (!$ReportPath) { $ReportPath = Join-Path $repo '.builds/combatant-stage5/game-resource-audit.json' }
$reportFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportPath)
$gameDll = Join-Path $gameRootPath 'RimWorldWin64_Data/Managed/Assembly-CSharp.dll'
$report = [ordered]@{
    StartedAtUtc = [DateTime]::UtcNow.ToString('o'); Success = $false
    Scope = 'Production XML and real constructors / AbilityComp.Initialize. Not full game XML loading, Ability.Initialize, effect application, combat or Scribe.'
    Inputs = [ordered]@{}; Checks = [Collections.Generic.List[string]]::new(); Error = $null
}
function Require([bool]$condition, [string]$message) {
    if (!$condition) { throw $message }
}
function Read-Xml([string]$path) { [xml](Get-Content -LiteralPath $path -Raw -Encoding utf8) }
function Record([string]$name) { $report.Checks.Add($name); Write-Output "PASS $name" }
function Number([string]$value) { [single]::Parse($value, [Globalization.CultureInfo]::InvariantCulture) }
function NodeText($node, [string]$path) {
    $found = $node.SelectSingleNode($path)
    Require ($null -ne $found -and ![string]::IsNullOrWhiteSpace($found.InnerText)) "Missing XML value: $path"
    $found.InnerText
}
try {
    foreach ($path in @($gameDll, $ModAssemblyPath)) {
        $file = Get-Item -LiteralPath $path
        $report.Inputs[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    $sourceDefs = @{}
    $languageDocs = @{}
    $xmlCount = 0
    foreach ($folder in @('About','Defs','Languages','Patches')) {
        if ($folder -eq 'Patches' -and !(Test-Path -LiteralPath (Join-Path $repo $folder))) { continue }
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo $folder) -Filter '*.xml' -Recurse -File) {
            $doc = Read-Xml $file.FullName
            $report.Inputs[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
            $xmlCount++
            if ($folder -eq 'Defs') {
                foreach ($node in $doc.SelectNodes('/Defs/*[defName]')) {
                    $name = $node.defName
                    if (!$sourceDefs.ContainsKey($name)) { $sourceDefs[$name] = @() }
                    $sourceDefs[$name] += $node
                }
            }
            if ($folder -eq 'Languages') { $languageDocs[$file.FullName] = $doc }
        }
    }
    $report.XmlFiles = $xmlCount
    Record "XML syntax ($xmlCount files)"
    function SourceDef([string]$name) {
        Require ($sourceDefs.ContainsKey($name) -and $sourceDefs[$name].Count -eq 1) "Missing or duplicate Def: $name"
        $sourceDefs[$name][0]
    }
    $ability = SourceDef 'SSC_CombatOverdrive'
    # ContentFinder resolves iconPath relative to Textures, without the extension.
    $textureRoot = [IO.Path]::GetFullPath((Join-Path $repo 'Textures'))
    $iconFile = [IO.Path]::GetFullPath((Join-Path $textureRoot ((NodeText $ability 'iconPath') + '.png')))
    Require ($iconFile.StartsWith($textureRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) 'Icon path escapes Textures'
    Require (Test-Path -LiteralPath $iconFile -PathType Leaf) 'Missing Combat Overdrive icon'
    $report.Inputs[$iconFile] = (Get-FileHash -LiteralPath $iconFile -Algorithm SHA256).Hash
    Record 'Combat Overdrive texture reference'
    $ordinary = SourceDef 'SSC_Hediff_Combatant'
    $final = SourceDef 'SSC_Hediff_Combatant_Final'
    $buff = SourceDef 'SSC_Hediff_CombatOverdrive'
    $recipe = SourceDef 'SSC_PSEdit_Combatant_Final'
    $research = SourceDef 'SSC_RES_Combatant'
    foreach ($node in @($ability,$ordinary,$final,$buff,$recipe,$research)) {
        foreach ($field in @('label','description')) { $null = NodeText $node $field }
    }
    $effect = $ability.SelectSingleNode('comps/li')
    Require ($ability.SelectNodes('comps/li').Count -eq 1) 'Expected one ability effect'
    Require ((NodeText $effect 'hediffDef') -eq $buff.defName) 'Ability points to the wrong buff'
    Require ((NodeText $final 'comps/li/abilityDef') -eq $ability.defName) 'Final marker grants the wrong ability'
    Require ((NodeText $recipe 'hediffToAdd') -eq $final.defName) 'Recipe adds the wrong final marker'
    Require ((NodeText $recipe 'researchPrerequisite') -eq $research.defName) 'Recipe research mismatch'
    $null = SourceDef (NodeText $research 'prerequisites/li')
    $ingredients = @($recipe.SelectNodes('ingredients/li/filter/thingDefs/li') | ForEach-Object InnerText)
    $fixedIngredients = @($recipe.SelectNodes('fixedIngredientFilter/thingDefs/li') | ForEach-Object InnerText)
    Require ($ingredients.Count -eq 8 -and ($ingredients | Select-Object -Unique).Count -eq 8) 'Expected eight distinct gel bases'
    Require (($ingredients -join '|') -eq ($fixedIngredients -join '|')) 'Recipe ingredient filters differ'
    foreach ($name in $ingredients) { $null = SourceDef $name }
    $coreDefs = @{}
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $gameRootPath 'Data/Core/Defs') -Filter '*.xml' -Recurse -File) {
        $doc = Read-Xml $file.FullName
        foreach ($node in $doc.SelectNodes('/Defs/*[defName]')) { $coreDefs[$node.defName] = $node.LocalName }
    }
    foreach ($pair in @(@((NodeText $recipe 'recipeUsers/li'),'ThingDef'),@((NodeText $ability 'jobDef'),'JobDef'),@((NodeText $recipe 'workSpeedStat'),'StatDef'))) {
        Require ($coreDefs[$pair[0]] -eq $pair[1]) "Invalid Core reference: $($pair[0])"
    }
    foreach ($node in @($ordinary,$final,$buff,$ability)) {
        foreach ($stat in $node.SelectNodes('stages/li/statOffsets/* | stages/li/statFactors/* | statBases/*')) {
            Require ($coreDefs[$stat.LocalName] -eq 'StatDef') "Invalid Core stat: $($stat.LocalName)"
        }
    }
    Record 'Ability / hediff / recipe / research / gel / Core stat references'

    $translations = @{
        'AbilityDef' = @('SSC_CombatOverdrive.label','SSC_CombatOverdrive.description')
        'HediffDef' = @('SSC_Hediff_Combatant.label','SSC_Hediff_Combatant.description','SSC_Hediff_Combatant.stages.0.label','SSC_Hediff_Combatant.stages.1.label','SSC_Hediff_Combatant.stages.2.label','SSC_Hediff_Combatant_Final.label','SSC_Hediff_Combatant_Final.description','SSC_Hediff_CombatOverdrive.label','SSC_Hediff_CombatOverdrive.description')
        'RecipeDef' = @('SSC_PSEdit_Combatant_Final.label','SSC_PSEdit_Combatant_Final.description','SSC_PSEdit_Combatant_Final.jobString')
        'ResearchProjectDef' = @('SSC_RES_Combatant.label','SSC_RES_Combatant.description')
    }
    $requiredKeys = @('SSC_ITab_SpecializationCombatant','SSC_ITab_SelectSpecializationCombatant','SSC_ITab_SpecializationCombatantDisabledIdentity','SSC_ITab_SpecializationCombatantDisabledResearch','SSC_ITab_SpecializationOrdinaryComplete','SSC_ITab_CombatantFinalizationTip','SSC_CombatOverdriveRequiresDrafted')
    foreach ($lang in @('ChineseSimplified','ChineseTraditional','English','Russian')) {
        $keys = @{}
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo "Languages/$lang/Keyed") -Filter '*.xml' -Recurse -File) {
            foreach ($node in $languageDocs[$file.FullName].SelectNodes('/LanguageData/*')) {
                Require (!$keys.ContainsKey($node.LocalName)) "Duplicate Keyed entry: $lang / $($node.LocalName)"
                $keys[$node.LocalName] = $node.InnerText
            }
        }
        foreach ($key in $requiredKeys) { Require (![string]::IsNullOrWhiteSpace($keys[$key])) "Missing Keyed translation: $lang / $key" }
        if ($lang -eq 'ChineseSimplified') { continue }
        foreach ($kind in $translations.Keys) {
            $entries = @{}
            foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repo "Languages/$lang/DefInjected/$kind") -Filter '*.xml' -Recurse -File) {
                foreach ($node in $languageDocs[$file.FullName].SelectNodes('/LanguageData/*')) {
                    if ($node.LocalName -notin $translations[$kind]) { continue }
                    Require (!$entries.ContainsKey($node.LocalName)) "Duplicate combatant translation: $lang / $($node.LocalName)"
                    $entries[$node.LocalName] = $node.InnerText
                }
            }
            foreach ($key in $translations[$kind]) { Require (![string]::IsNullOrWhiteSpace($entries[$key])) "Missing DefInjected translation: $lang / $key" }
        }
    }
    Record 'Four-language combatant coverage and duplicate translation keys'

    # Only resolve the required types. Loading all mod types would unnecessarily require every optional dependency.
    $game = [Reflection.Assembly]::LoadFrom($gameDll)
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssemblyPath).Path)
    function GameType([string]$name) {
        foreach ($prefix in @('','RimWorld.','Verse.')) {
            $type = $game.GetType($prefix + $name, $false)
            if ($null -ne $type) { return $type }
        }
        throw "Missing game type: $name"
    }
    function CheckFields([Type]$type, $node) {
        foreach ($field in $node.SelectNodes('*')) { Require ($null -ne $type.GetField($field.LocalName)) "Invalid field: $($type.FullName).$($field.LocalName)" }
    }
    CheckFields (GameType 'AbilityDef') $ability
    CheckFields (GameType 'VerbProperties') $ability.verbProperties
    CheckFields (GameType 'TargetingParameters') $ability.verbProperties.targetParams
    $propsType = GameType $effect.GetAttribute('Class')
    CheckFields $propsType $effect
    $props = [Activator]::CreateInstance($propsType)
    $effectType = GameType (NodeText $effect 'compClass')
    Require ((GameType 'AbilityComp').IsAssignableFrom($effectType)) 'Effect type must derive from AbilityComp'
    $propsType.GetField('compClass').SetValue($props, $effectType)
    $buffDef = [Activator]::CreateInstance((GameType 'HediffDef'))
    (GameType 'HediffDef').GetField('defName').SetValue($buffDef, [string]$buff.defName)
    $propsType.GetField('hediffDef').SetValue($props, $buffDef)
    foreach ($name in @('onlyApplyToSelf','replaceExisting')) { $propsType.GetField($name).SetValue($props,[bool]::Parse((NodeText $effect $name))) }
    $seconds = Number (NodeText $effect 'durationSecondsOverride')
    $durationField = $propsType.GetField('durationSecondsOverride')
    $durationRange = [Activator]::CreateInstance($durationField.FieldType, [object[]]@($seconds,$seconds))
    $durationField.SetValue($props,$durationRange)
    $component = [Activator]::CreateInstance($effectType)
    # Reflection avoids PowerShell's case-insensitive collision between props and Props.
    $effectType.GetMethod('Initialize').Invoke($component,[object[]]@($props)) | Out-Null
    Require ([object]::ReferenceEquals($props,$effectType.GetField('props').GetValue($component))) 'AbilityComp.Initialize did not retain its properties'
    Record "Real ability effect constructor and Initialize: $($effectType.FullName)"

    $abilityType = $mod.GetType((NodeText $ability 'abilityClass'), $true)
    Require ((GameType 'Ability').IsAssignableFrom($abilityType)) 'Custom ability does not derive from Ability'
    foreach ($argsList in @(@(),@((GameType 'Pawn')),@((GameType 'Pawn'),(GameType 'AbilityDef')))) {
        Require ($null -ne $abilityType.GetConstructor([Type[]]$argsList)) 'Missing required ability constructor'
    }
    $null = [Activator]::CreateInstance($abilityType) # No pawn, tracker, or game-wide ID initialization.
    $grantNode = $final.SelectSingleNode('comps/li')
    $grantType = $mod.GetType($grantNode.GetAttribute('Class'),$true)
    CheckFields $grantType $grantNode
    $grant = [Activator]::CreateInstance($grantType)
    $grantComponentType = $grantType.GetField('compClass').GetValue($grant)
    Require ($null -ne $grantComponentType -and (GameType 'HediffComp').IsAssignableFrom($grantComponentType)) 'Grant component type is invalid'
    $null = [Activator]::CreateInstance($grantComponentType)
    Record 'Custom ability constructors and final marker grant component'

    foreach ($node in @($final,$buff)) {
        Require ((NodeText $node 'hediffClass') -eq 'HediffWithComps') 'Hediff components require HediffWithComps'
        CheckFields (GameType 'HediffDef') $node
        foreach ($stage in $node.SelectNodes('stages/li')) { CheckFields (GameType 'HediffStage') $stage }
    }
    $timerNode = $buff.SelectSingleNode('comps/li')
    $timerType = GameType $timerNode.GetAttribute('Class')
    CheckFields $timerType $timerNode
    $timer = [Activator]::CreateInstance($timerType)
    $timerComponentType = $timerType.GetField('compClass').GetValue($timer)
    Require ($null -ne $timerComponentType -and $timerComponentType.FullName -eq 'Verse.HediffComp_Disappears') 'Missing disappearing component'
    $null = [Activator]::CreateInstance($timerComponentType)
    $durationMethod = (GameType 'GenTicks').GetMethod('SecondsToTicks',[Type[]]@([single]))
    $effectTicks = $durationMethod.Invoke($null,[object[]]@($seconds))
    $displayTicks = $durationMethod.Invoke($null,[object[]]@((Number (NodeText $ability 'statBases/Ability_Duration'))))
    Require ($effectTicks -eq 2500 -and $displayTicks -eq 2500 -and (NodeText $timerNode 'disappearsAfterTicks') -eq '2500') 'Duration must be 2500 ticks throughout'
    Require ((NodeText $ability 'cooldownTicksRange') -eq '10000') 'Cooldown must be 10000 ticks'
    $report.EffectTicks = $effectTicks
    $report.DisplayTicks = $displayTicks
    Record 'Real disappearance component and GenTicks duration conversion'
    $report.Success = $true
} catch {
    $report.Error = $_.Exception.ToString()
    throw
} finally {
    $report.FinishedAtUtc = [DateTime]::UtcNow.ToString('o')
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportFile)) | Out-Null
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFile -Encoding utf8
    Write-Output "Report: $reportFile"
}
