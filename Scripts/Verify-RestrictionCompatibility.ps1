#requires -Version 7.0
<#
.SYNOPSIS
只读核验限制系统编译产物与指定游戏、RJW 及可选模组的接口，并生成 JSON 报告。
.DESCRIPTION
需要调用者显式提供模组、游戏、RJW、Mono.Cecil 及报告路径。Genes、Onahole 未提供时会明确记录未检查，
不会把本机缺少的可选 DLL 当成已核验成功。只加载 Cecil 工具库，游戏和模组 DLL 全部只读取元数据，
不执行它们的代码，不启动 Unity，不安装 Harmony 补丁，也不代替实际游戏兼容测试。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ModAssembly,
    [Parameter(Mandatory)][string]$GameAssembly,
    [Parameter(Mandatory)][string]$RjwAssembly,
    [string]$GenesAssembly,
    [string]$OnaholeAssembly,
    [Parameter(Mandatory)][string]$CecilAssembly,
    [Parameter(Mandatory)][string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$startedAt = [DateTime]::UtcNow
$reportFile = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportPath)
$checks = [Collections.Generic.List[object]]::new()
$inputs = [Collections.Generic.List[object]]::new()
$assemblies = @{}
$types = @{}
$reservationOverrides = [Collections.Generic.List[string]]::new()
$legacyAccess = [Collections.Generic.List[string]]::new()

<#
.SYNOPSIS
解析调用者提供的文件路径；显式提供但不存在的 DLL 必须报错，不能悄悄降级为可选缺席。
#>
function Resolve-AssemblyFile {
    param([string]$Path)
    $item = Get-Item -LiteralPath $Path -ErrorAction Stop
    if ($item.PSIsContainer) { throw "需要程序集文件，实际为目录：$Path" }
    return $item.FullName
}

<#
.SYNOPSIS
递归枚举顶层及嵌套类型，避免编译器生成的闭包或嵌套驱动逃过元数据检查。
#>
function Get-AllTypes {
    param($Roots)
    foreach ($type in $Roots) {
        $type
        if ($type.HasNestedTypes) { Get-AllTypes -Roots $type.NestedTypes }
    }
}

<#
.SYNOPSIS
记录一个独立核验的通过、失败或明确跳过；单项失败不隐藏其他可以继续进行的检查。
#>
function Invoke-Verification {
    param([string]$Name, [string]$Category, [scriptblock]$Action, [string]$SkipReason)
    if ($SkipReason) {
        $checks.Add([pscustomobject]@{ Name = $Name; Category = $Category; Status = 'Skipped'; Details = @($SkipReason) })
        return
    }
    try {
        $details = @(& $Action)
        $checks.Add([pscustomobject]@{ Name = $Name; Category = $Category; Status = 'Passed'; Details = $details })
    }
    catch {
        $checks.Add([pscustomobject]@{ Name = $Name; Category = $Category; Status = 'Failed'; Details = @($_.Exception.Message) })
    }
}

<#
.SYNOPSIS
返回唯一声明的方法，缺失或产生歧义时拒绝猜测目标，便于发现依赖版本变化。
#>
function Get-DeclaredMethod {
    param([string]$TypeName, [string]$MethodName)
    if (-not $types.ContainsKey($TypeName)) { throw "缺少目标类型：$TypeName" }
    $methods = @($types[$TypeName].Methods | Where-Object Name -eq $MethodName)
    if ($methods.Count -ne 1) { throw "目标方法缺失或有歧义：${TypeName}::$MethodName，数量=$($methods.Count)" }
    return $methods[0]
}

<#
.SYNOPSIS
沿已加载的元数据类型表判断继承关系，不调用 Resolve，也不隐式加载未提供的依赖程序集。
#>
function Test-Inheritance {
    param([string]$TypeName, [string]$ExpectedBase)
    $seen = [Collections.Generic.HashSet[string]]::new()
    while ($TypeName -and $seen.Add($TypeName)) {
        if ($TypeName -eq $ExpectedBase) { return $true }
        if (-not $types.ContainsKey($TypeName) -or -not $types[$TypeName].BaseType) { return $false }
        $TypeName = $types[$TypeName].BaseType.FullName
    }
    return $false
}

<#
.SYNOPSIS
读取 Harmony 私有字段注入的声明类型，允许字段定义在已提供的基类中。
#>
function Get-FieldTypeName {
    param([string]$TypeName, [string]$FieldName)
    $seen = [Collections.Generic.HashSet[string]]::new()
    while ($TypeName -and $types.ContainsKey($TypeName) -and $seen.Add($TypeName)) {
        $type = $types[$TypeName]
        $fields = @($type.Fields | Where-Object Name -eq $FieldName)
        if ($fields.Count -eq 1) { return $fields[0].FieldType.FullName }
        $TypeName = if ($type.BaseType) { $type.BaseType.FullName } else { $null }
    }
    return $null
}

<#
.SYNOPSIS
核对固定补丁的实例、返回值、运行标记、私有字段及原方法参数注入，发现类型变化即报失败。
#>
function Test-PatchSignature {
    param([string]$PatchType, [string]$PatchMethod, [string]$TargetType, [string]$TargetMethod)
    $patch = Get-DeclaredMethod -TypeName ('SexSlaveCraft.' + $PatchType) -MethodName $PatchMethod
    $original = Get-DeclaredMethod -TypeName $TargetType -MethodName $TargetMethod
    if (-not $patch.IsStatic) { throw "补丁必须为静态方法：$($patch.FullName)" }
    foreach ($parameter in $patch.Parameters) {
        $actual = $parameter.ParameterType.FullName.TrimEnd('&')
        $expected = $null
        if ($parameter.Name -eq '__instance') {
            if ($original.IsStatic) { throw "静态原方法不能注入 __instance：$($original.FullName)" }
            if (Test-Inheritance -TypeName $TargetType -ExpectedBase $actual) { $expected = $actual }
        }
        elseif ($parameter.Name -eq '__result') { $expected = $original.ReturnType.FullName }
        elseif ($parameter.Name -eq '__runOriginal') { $expected = 'System.Boolean' }
        elseif ($parameter.Name.StartsWith('___')) {
            $expected = Get-FieldTypeName -TypeName $TargetType -FieldName $parameter.Name.Substring(3)
        }
        else {
            $matches = @($original.Parameters | Where-Object Name -eq $parameter.Name)
            if ($matches.Count -eq 1) { $expected = $matches[0].ParameterType.FullName.TrimEnd('&') }
        }
        if (-not $expected -or $actual -ne $expected) {
            throw "注入参数不匹配：$($patch.FullName)，$($parameter.Name)，实际=$actual，预期=$expected，目标=$($original.FullName)"
        }
    }
    return "$($patch.FullName) -> $($original.FullName)"
}

<#
.SYNOPSIS
确认自有事务方法直接调用指定服务，避免删除 Harmony 后缀后漏掉初始化或授权提交。
#>
function Test-ExplicitCall {
    param([string]$CallerType, [string]$CallerMethod, [string]$CalleeType, [string]$CalleeMethod)
    $method = Get-DeclaredMethod -TypeName $CallerType -MethodName $CallerMethod
    if (-not $method.HasBody) { throw "调用者没有 IL 方法体：$($method.FullName)" }
    $calls = @($method.Body.Instructions | Where-Object {
        $_.OpCode.Code.ToString() -in @('Call', 'Callvirt') -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq $CalleeType -and $_.Operand.Name -eq $CalleeMethod
    })
    if ($calls.Count -ne 1) { throw "必须且只能显式调用一次：$($method.FullName) -> ${CalleeType}::$CalleeMethod，数量=$($calls.Count)" }
    return "$($method.FullName) -> $($calls[0].Operand.FullName)"
}

try {
    $cecilPath = Resolve-AssemblyFile -Path $CecilAssembly
    Add-Type -Path $cecilPath
    $inputs.Add([pscustomobject]@{ Role = 'Cecil'; Status = 'LoadedTool'; Path = $cecilPath; SHA256 = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash })
    foreach ($entry in @(
        @{ Role = 'Mod'; Path = $ModAssembly }, @{ Role = 'Game'; Path = $GameAssembly },
        @{ Role = 'Rjw'; Path = $RjwAssembly }, @{ Role = 'Genes'; Path = $GenesAssembly },
        @{ Role = 'Onahole'; Path = $OnaholeAssembly }
    )) {
        if (-not $entry.Path) {
            $inputs.Add([pscustomobject]@{ Role = $entry.Role; Status = 'NotProvided'; Path = $null; SHA256 = $null })
            continue
        }
        $path = Resolve-AssemblyFile -Path $entry.Path
        # Cecil 只解析 PE/IL。这里不能用 Add-Type 或 Assembly.Load 加载游戏及模组代码。
        $assemblies[$entry.Role] = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
        $inputs.Add([pscustomobject]@{ Role = $entry.Role; Status = 'MetadataRead'; Path = $path; SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash })
        foreach ($type in Get-AllTypes -Roots $assemblies[$entry.Role].MainModule.Types) {
            if ($type.Name -eq '<Module>') { continue }
            # 每个程序集都有自己的编译器数据类型/兼容特性；它们不参与本脚本的游戏继承和目标定位。
            # 仍在逐程序集 IL 扫描中检查其方法，不因这里不建立名字索引而遗漏旧字段访问。
            if ($type.FullName.StartsWith('<') -or $type.Namespace -in @('System.Runtime.CompilerServices', 'Microsoft.CodeAnalysis')) { continue }
            if ($types.ContainsKey($type.FullName)) { throw "提供的程序集包含重复类型，无法唯一核验：$($type.FullName)" }
            $types[$type.FullName] = $type
        }
    }

    # 数组最后一项注明可选依赖；缺席时保留逐入口跳过记录，而不是缩短清单后声称全部通过。
    $fixedHooks = @(
        @('SSCRestrictionLovinReservationHook', 'Prefix', 'RimWorld.JobDriver_Lovin', 'TryMakePreToilReservations', ''),
        @('SSCRestrictionLovinToilsHook', 'Postfix', 'RimWorld.JobDriver_Lovin', 'MakeNewToils', ''),
        @('SSCRestrictionLovinSaveHook', 'Postfix', 'RimWorld.JobDriver_Lovin', 'ExposeData', ''),
        @('SSCRestrictionJobCleanupHook', 'Prefix', 'Verse.AI.JobDriver', 'Cleanup', ''),
        @('SSCRestrictionJobCleanupHook', 'Postfix', 'Verse.AI.JobDriver', 'Cleanup', ''),
        @('SSCRestrictionQuickiePreparationHook', 'Postfix', 'rjw.JobDriver_BestialityForFemale', 'MakeNewToils', ''),
        @('SSCRestrictionSeducedReservationHook', 'Prefix', 'RJW_Genes.JobDriver_Seduced', 'TryMakePreToilReservations', 'Genes'),
        @('SSCRestrictionSeduceValidHook', 'Postfix', 'RJW_Genes.CompAbilityEffect_Seduce', 'Valid', 'Genes'),
        @('SSCRestrictionSeduceApplyHook', 'Prefix', 'RJW_Genes.CompAbilityEffect_Seduce', 'Apply', 'Genes'),
        @('SSCRestrictionSceneSaveHook', 'Postfix', 'rjw.JobDriver_Sex', 'ExposeData', ''),
        @('SSCRestrictionQuickiePreparationHook', 'Postfix', 'rjw.JobDriver_SexQuick', 'MakeNewToils', ''),
        @('SSCRestrictionOrderedJobHook', 'Prefix', 'Verse.AI.Pawn_JobTracker', 'TryTakeOrderedJob', ''),
        @('SSCRestrictionAutomaticJobHook', 'Postfix', 'Verse.AI.ThinkNode_JobGiver', 'TryIssueJobPackage', ''),
        @('Patch_JobDriver_Sex_ProtectChainOfSexSlave', 'Prefix', 'rjw.JobDriver_Sex', 'TryMakePreToilReservations', ''),
        @('Patch_JobDriver_Sex_ProtectChainOfSexSlave', 'NextToil_Prefix', 'Verse.AI.JobDriver', 'TryActuallyStartNextToil', ''),
        @('Patch_JobDriver_Sex_ProtectChainOfSexSlave', 'Start_Prefix', 'rjw.JobDriver_SexBaseInitiator', 'Start', ''),
        @('Patch_JobDriver_Sex_ProtectChainOfSexSlave', 'Start_Postfix', 'rjw.JobDriver_SexBaseInitiator', 'Start', ''),
        @('Patch_JobDriver_Sex_ProtectChainOfSexSlave', 'End_Prefix', 'rjw.JobDriver_SexBaseInitiator', 'End', '')
    )
    foreach ($hook in $fixedHooks) {
        $skip = if ($hook[4] -and -not $assemblies.ContainsKey($hook[4])) { "$($hook[4]) 程序集未提供；此兼容入口未核验。" } else { $null }
        Invoke-Verification -Name "$($hook[0]).$($hook[1]) -> $($hook[2]).$($hook[3])" -Category 'FixedHook' -SkipReason $skip -Action {
            Test-PatchSignature -PatchType $hook[0] -PatchMethod $hook[1] -TargetType $hook[2] -TargetMethod $hook[3]
        }
    }

    foreach ($role in @('Mod', 'Rjw', 'Genes', 'Onahole')) {
        $skip = if (-not $assemblies.ContainsKey($role)) { "$role 程序集未提供；其预约重写未核验。" } else { $null }
        Invoke-Verification -Name "$role reservation overrides" -Category 'ReservationDiscovery' -SkipReason $skip -Action {
            $count = 0
            foreach ($type in Get-AllTypes -Roots $assemblies[$role].MainModule.Types) {
                if ($type.FullName -eq 'rjw.JobDriver_Sex' -or -not (Test-Inheritance -TypeName $type.FullName -ExpectedBase 'rjw.JobDriver_Sex')) { continue }
                foreach ($method in $type.Methods | Where-Object { $_.Name -eq 'TryMakePreToilReservations' -and -not $_.IsAbstract }) {
                    if ($method.IsStatic -or $method.ReturnType.FullName -ne 'System.Boolean' -or
                        $method.Parameters.Count -ne 1 -or $method.Parameters[0].ParameterType.FullName -ne 'System.Boolean') {
                        throw "预约重写签名变化：$($method.FullName)"
                    }
                    Test-PatchSignature -PatchType 'SSCRestrictionReservationHook' -PatchMethod 'Prefix' -TargetType $type.FullName -TargetMethod $method.Name
                    $reservationOverrides.Add($method.FullName)
                    $count++
                }
            }
            "已检查 $count 个声明的预约重写。"
        }
    }
    Invoke-Verification -Name 'Required reservation overrides' -Category 'ReservationDiscovery' -Action {
        foreach ($name in @('JobDriver_Masturbate', 'JobDriver_Training', 'JobDriver_RitualTraining', 'JobDriver_PE', 'JobDriver_SexBaseInitiator', 'JobDriver_SexQuick')) {
            if (-not @($reservationOverrides | Where-Object { $_ -match ([regex]::Escape('.' + $name + '::TryMakePreToilReservations')) }).Count) {
                throw "必须覆盖的预约重写未找到：$name"
            }
        }
        '六个必需的单人、普通、快速及 SSC 专用预约重写均已发现。'
    }

    Invoke-Verification -Name 'Retired types absent' -Category 'Cleanup' -Action {
        foreach ($name in @('SSCSexInteractionPolicy', 'SSCSexInteractionDecision', 'CompSSRapeCheck', 'CompProperties_SSRapeCheck',
            'SSCRestrictionBindHook', 'SSCRestrictionAssignHook')) {
            if ($types.ContainsKey('SexSlaveCraft.' + $name)) { throw "旧类型仍在编译产物中：$name" }
            $name
        }
    }
    Invoke-Verification -Name 'Legacy fields isolated to migration' -Category 'Cleanup' -Action {
        $legacyNames = @('allowOthersForTrainingOrSex', 'allowSexSlaveRape', 'protectBusAggressorRape', 'protectChainedAggressorRape', 'protectNonRapeOwnerOnly')
        $allowed = @('SexSlaveCraft.SSCRestrictionLegacySettings::Capture', 'SexSlaveCraft.CompSexSlaveTraining::ExposeRestrictions',
            'SexSlaveCraft.CompSexSlaveTraining::.ctor', 'SexSlaveCraft.SSCSettings::ExposeRestrictionSettings', 'SexSlaveCraft.SSCSettings::.ctor')
        foreach ($type in Get-AllTypes -Roots $assemblies['Mod'].MainModule.Types) {
            foreach ($method in $type.Methods) {
                if (-not $method.HasBody) { continue }
                foreach ($instruction in $method.Body.Instructions) {
                    $field = $instruction.Operand
                    if ($field -isnot [Mono.Cecil.FieldReference] -or $field.Name -notin $legacyNames) { continue }
                    if (($type.FullName + '::' + $method.Name) -notin $allowed) {
                        throw "运行逻辑仍访问迁移旧字段：$($method.FullName) -> $($field.FullName)"
                    }
                    $legacyAccess.Add("$($method.FullName) -> $($field.FullName)")
                }
            }
        }
        $legacyAccess.ToArray()
    }
    Invoke-Verification -Name 'Bind explicitly notifies lifecycle' -Category 'OwnedTransaction' -Action {
        Test-ExplicitCall -CallerType 'SexSlaveCraft.SSCBondUtility' -CallerMethod 'Bind' -CalleeType 'SexSlaveCraft.SSCRestrictionGameComponent' -CalleeMethod 'Notify'
    }
    Invoke-Verification -Name 'Assignment explicitly submits authorization' -Category 'OwnedTransaction' -Action {
        Test-ExplicitCall -CallerType 'SexSlaveCraft.SSCBondUtility' -CallerMethod 'TryAssignTrainer' -CalleeType 'SexSlaveCraft.SSCRestrictionTrainerAssignment' -CalleeMethod 'TryAssign'
    }
    Invoke-Verification -Name 'Optional integrations have no hard references' -Category 'Dependency' -Action {
        $references = @($assemblies['Mod'].MainModule.AssemblyReferences.Name)
        $optionalNames = @('Rjw-Genes', 'RJW_Onahole')
        foreach ($role in @('Genes', 'Onahole')) {
            if ($assemblies.ContainsKey($role)) { $optionalNames += $assemblies[$role].Name.Name }
        }
        foreach ($reference in $references) {
            if ($reference -in $optionalNames) { throw "可选模组被写入硬引用：$reference" }
        }
        'Genes 与 Onahole 不在当前模组程序集的硬引用中；此检查不要求它们已安装。'
    }
}
catch {
    $checks.Add([pscustomobject]@{ Name = 'Metadata setup'; Category = 'Input'; Status = 'Failed'; Details = @($_.Exception.Message) })
}
finally {
    foreach ($assembly in $assemblies.Values) { $assembly.Dispose() }
    $failed = @($checks | Where-Object Status -eq 'Failed')
    $report = [ordered]@{
        StartedUtc = $startedAt.ToString('o'); FinishedUtc = [DateTime]::UtcNow.ToString('o')
        Success = $failed.Count -eq 0
        Scope = 'Metadata only; no game code, Harmony patch installation, Unity or live-game execution.'
        Inputs = $inputs.ToArray()
        Summary = [ordered]@{
            Passed = @($checks | Where-Object Status -eq 'Passed').Count
            Failed = $failed.Count; Skipped = @($checks | Where-Object Status -eq 'Skipped').Count
            FixedHooksPassed = @($checks | Where-Object { $_.Category -eq 'FixedHook' -and $_.Status -eq 'Passed' }).Count
            FixedHooksSkipped = @($checks | Where-Object { $_.Category -eq 'FixedHook' -and $_.Status -eq 'Skipped' }).Count
            ReservationOverrides = $reservationOverrides.Count
        }
        Checks = $checks.ToArray(); ReservationOverrides = $reservationOverrides.ToArray(); LegacyFieldAccess = $legacyAccess.ToArray()
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportFile)) | Out-Null
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFile -Encoding utf8
}

Write-Output "限制兼容元数据核验：通过=$($report.Summary.Passed)，失败=$($report.Summary.Failed)，明确跳过=$($report.Summary.Skipped)。报告：$reportFile"
if (-not $report.Success) { throw '限制兼容元数据核验失败；请查看 JSON 报告中的 Failed 项。' }
