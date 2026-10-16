#requires -Version 7.0
# 只测试元数据签名判断；VerificationScript 可指向隔离的修复前版本以证明旧实现确实失败。
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [string]$VerificationScript = (Join-Path $RepositoryRoot 'Scripts/Verify-RestrictionCompatibility.ps1')
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Load production functions through the parser without invoking the CLI or loading game DLLs.
# 通过 AST 精确选择生产函数，避免点导入整份脚本后触发 CLI 参数校验或读取实际游戏程序集。
$ast = [Management.Automation.Language.Parser]::ParseFile($VerificationScript, [ref]$null, [ref]$null)
foreach ($name in @('Get-DeclaredMethod', 'Test-Inheritance', 'Get-FieldTypeName', 'Test-PatchSignature')) {
    # 同名函数必须唯一；签名或组织结构变化时明确失败，不能悄悄改用测试内的重写实现。
    $function = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true))
    if ($function.Count -ne 1) { throw "Missing unique production function: $name" }
    Invoke-Expression $function[0].Extent.Text
}
$checks = 0
# 构造 Cecil 参数所需的最小形状；FullName 末尾的 & 对应 out/ref 的底层元数据表示。
function Param([string]$Name, [string]$Type) {
    [pscustomobject]@{ Name=$Name; ParameterType=[pscustomobject]@{FullName=$Type} }
}
# 默认提供静态方法和空特性集合，测试只改变当前用例关心的生产者或消费者条件。
function Method([string]$Name, [object[]]$Parameters, [bool]$Static = $true) {
    [pscustomobject]@{ Name=$Name; FullName="Patch::$Name"; Parameters=@($Parameters); IsStatic=$Static; CustomAttributes=@(); DeclaringType=$null }
}
# 每个场景重建独立补丁类、原目标和类型表；Prefix/Postfix/Finalizer 共享同一个 DeclaringType。
function Fixture([string]$Produced = 'Context&', [string]$Consumed = 'Context', [string]$PrefixName = 'Prefix') {
    # 非约定名称通过 HarmonyPrefix 特性识别，覆盖改名后仍属合法 Prefix 的情况。
    $prefix = Method $PrefixName @((Param '__state' $Produced))
    if ($PrefixName -ne 'Prefix') { $prefix.CustomAttributes = @([pscustomobject]@{AttributeType=[pscustomobject]@{FullName='HarmonyLib.HarmonyPrefix'}}) }
    $postfix = Method 'Postfix' @((Param '__state' $Consumed))
    $finalizer = Method 'Finalizer' @((Param '__state' $Consumed))
    $owner = [pscustomobject]@{ FullName='SexSlaveCraft.Fixture'; Methods=@($prefix,$postfix,$finalizer) }
    # 这些反向引用是同类状态配对的依据，不能让不同补丁类通过同名字段意外共享状态。
    $owner.Methods | ForEach-Object { $_.DeclaringType = $owner }
    # 原目标只声明普通 Int32 参数 value，刻意没有 __state，以捕获旧实现的误判。
    $original = [pscustomobject]@{ Name='Run'; FullName='Original::Run'; IsStatic=$false; ReturnType=[pscustomobject]@{FullName='System.Void'}; Parameters=@((Param 'value' 'System.Int32')) }
    $script:types = @{ 'SexSlaveCraft.Fixture'=$owner; 'Original'=[pscustomobject]@{Methods=@($original);BaseType=$null} }
    return $owner
}
# 正例必须无异常，反例必须抛出异常；不能把“所有输入都拒绝”误当成验证器修复成功。
function Check([string]$Name, [scriptblock]$Action, [bool]$ShouldPass) {
    $errorText = $null
    try { $null = & $Action } catch { $errorText = $_.Exception.Message }
    if ($ShouldPass -and $errorText) { throw "$Name failed unexpectedly: $errorText" }
    if (-not $ShouldPass -and -not $errorText) { throw "$Name should reject invalid state/signature" }
    $script:checks++
    Write-Output "PASS $Name"
}
# 所有场景都调用实际 Test-PatchSignature，固定目标可避免用例间不相关的签名差异。
function Verify([string]$Method = 'Postfix') { Test-PatchSignature 'Fixture' $Method 'Original' 'Run' }
$null = Fixture
# 正例组：Prefix 生产状态，Postfix 和 Finalizer 消费相同底层类型。
Check 'Prefix byref state is accepted' { Verify 'Prefix' } $true
Check 'Postfix consumes matching state' { Verify } $true
Check 'Finalizer consumes matching state' { Verify 'Finalizer' } $true
# 消费者同样按引用传参仍应与底层状态类型匹配，不能把 & 当成另一种业务类型。
$null = Fixture 'Context&' 'Context&'
Check 'Byref consumer matches underlying state type' { Verify } $true
# 自定义方法名依靠特性识别，确保规则没有硬编码成只接受名为 Prefix 的方法。
$null = Fixture 'Context&' 'Context' 'BeforeRun'
Check 'HarmonyPrefix attribute identifies producer' { Verify } $true
# 反例组：先改变消费者类型，再分别移除生产者、改成非静态及加入第二个生产者。
$null = Fixture 'Context&' 'DifferentContext'
Check 'Different state type is rejected' { Verify } $false
$owner = Fixture; $owner.Methods = @($owner.Methods | Where-Object Name -ne 'Prefix')
Check 'Missing same-class producer is rejected' { Verify } $false
$owner = Fixture; $owner.Methods[0].IsStatic = $false
Check 'Nonstatic producer is rejected' { Verify } $false
$owner = Fixture; $extra = Method 'AnotherPrefix' @((Param '__state' 'Context&')); $extra.DeclaringType = $owner
$extra.CustomAttributes = @([pscustomobject]@{AttributeType=[pscustomobject]@{FullName='HarmonyLib.HarmonyPrefix'}})
$owner.Methods += $extra
Check 'Ambiguous producers are rejected' { Verify } $false
# 控制用例：普通原方法参数的类型错误仍须被拒绝，__state 特判不能放宽其他参数。
$owner = Fixture; $owner.Methods[1].Parameters += (Param 'value' 'System.String')
Check 'Ordinary original parameter mismatch is still rejected' { Verify } $false
# 父测试除退出码外还核对该固定完成标记，避免脚本仅执行了部分断言便返回成功。
Write-Output "State signature checks: $checks passed."
