#requires -Version 7.0
<#
.SYNOPSIS
使用目标 RimWorld 1.7 的实际编译器检查凝胶 GLSL，不启动游戏、不打包。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameRoot,
    [string]$DotnetPath = $env:SSC_DOTNET_PATH,
    [string]$ReportDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$GameRoot = (Resolve-Path -LiteralPath $GameRoot).Path
if (-not $ReportDirectory) { $ReportDirectory = Join-Path $repoRoot '.builds/gel-visual/shader' }
$ReportDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportDirectory)
if (-not $DotnetPath) {
    $portableName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $portable = Join-Path $repoRoot ".builds/version-layout/sdk/dotnet/$portableName"
    $DotnetPath = if (Test-Path -LiteralPath $portable) { $portable } else { 'dotnet' }
}
$saved = @{}
foreach ($name in @('DOTNET_CLI_HOME', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    # 隔离 SDK 首次运行文件，避免工具向用户配置目录写入 sentinel、证书或 PATH 设置。
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot '.builds/dotnet-cli-home'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    $arguments = @('run', '--project', (Join-Path $PSScriptRoot 'GelatinizationShader.csproj'),
        '--configuration', 'Release', "-p:GameRoot=$GameRoot",
        "-p:RestoreConfigFile=$(Join-Path $repoRoot 'Tests/NuGet.Config')", '--', $repoRoot, $GameRoot, $ReportDirectory)
    & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) { throw "Native shader verification failed: $LASTEXITCODE" }
}
finally {
    foreach ($name in $saved.Keys) {
        if ($null -eq $saved[$name]) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
        else { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    }
}
