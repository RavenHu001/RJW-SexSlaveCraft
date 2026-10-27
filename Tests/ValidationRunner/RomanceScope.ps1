#requires -Version 7.0
<#
.SYNOPSIS
只验证总回归的浪漫文案模式隔离及摘要门禁，不构建或执行游戏机制套件。
.DESCRIPTION
从真实 Test-All.ps1 的 AST 提取环境准备／finally 和执行结果判定区块；执行这些原区块，
以轻量子进程检查环境继承，以模拟宿主输出检查原 catch 是否阻断。不会复制另一套门禁算法。
#>
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reportRoot = Join-Path $repo ('.builds/romance-gate-isolation/' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$pwsh = (Get-Process -Id $PID).Path
$checks = 0

<# .SYNOPSIS 检查真实入口区块的运行结果，失败立即停止专项并保留日志。 #>
function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
    Write-Host "PASS $Message"
}

# 只提取明确的真实 AST 边界；入口重构后若边界不再唯一，应先更新专项，不能悄悄漏测。
$tokens = $null; $parseErrors = $null
$sourcePath = Join-Path $repo 'Scripts/Test-All.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($sourcePath, [ref]$tokens, [ref]$parseErrors)
Assert ($parseErrors.Count -eq 0) 'Test-All syntax is valid'
$top = @($ast.EndBlock.Statements)
$environmentStart = @($top | Where-Object { $_.Extent.Text -eq '$savedTestEnvironment = @{}' })
$outerTry = @($top | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })
Assert ($environmentStart.Count -eq 1 -and $outerTry.Count -eq 1) 'environment and outer try boundaries are unique'
$outerTry = $outerTry[0]
$suiteAssignment = @($outerTry.Body.Statements | Where-Object { $_.Extent.Text.StartsWith('$suites = @(') })
Assert ($suiteAssignment.Count -eq 1 -and $null -ne $outerTry.Finally) 'suite boundary and real finally exist'
$source = [IO.File]::ReadAllText($sourcePath)
$capture = $source.Substring($environmentStart[0].Extent.StartOffset,
    $outerTry.Extent.StartOffset - $environmentStart[0].Extent.StartOffset)
$prepare = $source.Substring($outerTry.Body.Extent.StartOffset + 1,
    $suiteAssignment[0].Extent.StartOffset - $outerTry.Body.Extent.StartOffset - 1)
$environmentBlock = [scriptblock]::Create(@"
param([scriptblock]`$TestBody, [string]`$repoRoot, [string]`$runtimeRootPath)
$capture
try {
$prepare
    & `$TestBody
}
finally $($outerTry.Finally.Extent.Text)
"@)

# 保存专项本身会触及的环境。入口成功／失败两条路径都必须恢复调用者的精确值。
$savedEnvironment = @{}
foreach ($variable in @('SSC_ROMANCE_TEXT_ONLY', 'SSC_TEST_REPOSITORY_ROOT', 'SSC_TEST_RUNTIME_ROOT',
    'SSC_TEST_COMMON_ROOT', 'DOTNET_CLI_HOME', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH')) {
    $savedEnvironment[$variable] = [Environment]::GetEnvironmentVariable($variable, 'Process')
}
try {
    foreach ($initialValue in @($null, '', '0', '1', 'custom')) {
        foreach ($fail in @($false, $true)) {
            [Environment]::SetEnvironmentVariable('SSC_ROMANCE_TEXT_ONLY', ($initialValue ?? [NullString]::Value), 'Process')
            # NullString 保留未设置与空串的差别，以实际调用者值为恢复基准。
            $expected = [Environment]::GetEnvironmentVariable('SSC_ROMANCE_TEXT_ONLY', 'Process')
            $caught = $null
            try {
                & $environmentBlock {
                    Assert ($null -eq [Environment]::GetEnvironmentVariable('SSC_ROMANCE_TEXT_ONLY', 'Process')) 'aggregate process clears text-only selection'
                    $childValue = & $pwsh -NoProfile -Command '[Environment]::GetEnvironmentVariable("SSC_ROMANCE_TEXT_ONLY", "Process") -eq $null'
                    Assert ($LASTEXITCODE -eq 0 -and "$childValue" -eq 'True') 'child process inherits full-suite environment'
                    if ($fail) { throw 'fixture environment failure' }
                } $repo (Join-Path $repo '1.7')
            }
            catch { $caught = $_.Exception.Message }
            Assert (($fail -and $caught -eq 'fixture environment failure') -or (-not $fail -and $null -eq $caught)) "success or injected failure propagates (caught: $caught)"
            Assert ([object]::Equals($expected, [Environment]::GetEnvironmentVariable('SSC_ROMANCE_TEXT_ONLY', 'Process'))) 'caller selection restored after aggregate scope'
        }
    }
}
finally {
    foreach ($variable in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($variable, ($savedEnvironment[$variable] ?? [NullString]::Value), 'Process')
    }
}

# 从实际套件 try 中提取执行输出到 Passed 的连续原区块，并保留原 catch／日志路径。
# 用模拟输出替代 dotnet 执行，不启动真实浪漫套件，更不运行总回归。
$suiteTry = @($ast.FindAll({ param($node)
    $node -is [Management.Automation.Language.TryStatementAst] -and
    @($node.Body.Statements | Where-Object { $_.Extent.Text.StartsWith('$output = Invoke-DotnetStep ') }).Count -eq 1
}, $true))
Assert ($suiteTry.Count -eq 1) 'suite output gate boundary is unique'
$suiteTry = $suiteTry[0]
$outputAssignment = @($suiteTry.Body.Statements | Where-Object { $_.Extent.Text.StartsWith('$output = Invoke-DotnetStep ') })[0]
$gate = $source.Substring($outputAssignment.Extent.StartOffset,
    $suiteTry.Body.Extent.EndOffset - 1 - $outputAssignment.Extent.StartOffset)
$gateBlock = [scriptblock]::Create(@"
param([string]`$fixtureOutput, [string]`$name, [string]`$logPath)
function Invoke-DotnetStep { param(`$CommandArguments, `$Stage, `$LogPath, `$Executable) return `$fixtureOutput }
`$testDll = 'fixture.dll'; `$testArguments = @(); `$suiteDotnet = 'fixture'
`$result = [pscustomobject]@{ Status = 'NotRun'; PassedCases = `$null; TotalCases = `$null; Reason = '' }
try {
$gate
}
$($suiteTry.CatchClauses[0].Extent.Text)
return `$result
"@)
$cases = @(
    @{ Name = 'normal dynamic total'; Output = 'RESULT: 17/17 passed'; Status = 'Passed'; Total = 17 },
    @{ Name = 'normal existing summary'; Output = '590/590 passed'; Status = 'Passed'; Total = 590 },
    @{ Name = 'text scope despite passing summary'; Output = "SCOPE: romance text only; no interaction/policy/personality regression`nRESULT: 590/590 passed"; Status = 'Failed'; Total = $null },
    @{ Name = 'text scope after summary'; Output = "RESULT: 9999/9999 passed`r`nSCOPE: romance text only; fixture"; Status = 'Failed'; Total = $null },
    @{ Name = 'missing summary'; Output = 'fixture'; Status = 'Failed'; Total = $null },
    @{ Name = 'ambiguous summary'; Output = "RESULT: 1/1 passed`nRESULT: 2/2 passed"; Status = 'Failed'; Total = $null },
    @{ Name = 'partial summary'; Output = 'RESULT: 1/2 passed'; Status = 'Failed'; Total = 2 },
    @{ Name = 'zero summary'; Output = 'RESULT: 0/0 passed'; Status = 'Failed'; Total = 0 }
)
foreach ($case in $cases) {
    $logPath = Join-Path $reportRoot ($case.Name.Replace(' ', '-') + '.log')
    $result = & $gateBlock $case.Output 'RomanceControlNative' $logPath
    Assert ($result.Status -eq $case.Status -and [object]::Equals($result.TotalCases, $case.Total)) $case.Name
    if ($case.Output.Contains('SCOPE:')) {
        Assert ($result.Reason -like '*text-only scope*' -and (Get-Content -LiteralPath $logPath -Raw).Contains($result.Reason)) 'text-only rejection reaches original failure receipt and log'
    }
}
# 检查两个入口的接线：发布继续调用受保护总入口，独立文案选择条件保持原样。
Assert ((Get-Content -LiteralPath (Join-Path $repo 'Scripts/New-Release.ps1') -Raw).Contains("Join-Path `$PSScriptRoot 'Test-All.ps1'")) 'release still delegates to aggregate gate'
Assert ((Get-Content -LiteralPath (Join-Path $repo 'Tests/RomanceControlNative/Program.cs') -Raw).Contains('Environment.GetEnvironmentVariable("SSC_ROMANCE_TEXT_ONLY") == "1"')) 'standalone text-only selection remains available'
[pscustomobject]@{ Scope = 'Romance aggregate gate only; no gameplay suites'; Passed = $checks; Total = $checks } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportRoot 'summary.json') -Encoding utf8
Write-Host "RESULT: $checks/$checks passed"
Write-Host "Reports: $reportRoot"
