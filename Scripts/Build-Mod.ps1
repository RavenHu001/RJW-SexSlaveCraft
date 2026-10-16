#requires -Version 7.0
<#
.SYNOPSIS
Build the 1.7 development DLL using explicit game and mod references.
.DESCRIPTION
Requires a .NET 10 SDK. References are never copied into the mod. The frozen
1.6/2.4.1 baseline is retained as-is; further development targets only 1.7.
SSC_GAME_ROOT, SSC_RJW_ASSEMBLY_PATH, SSC_ANIMATIONS_ASSEMBLY_PATH and
SSC_GAME_HARMONY_PATH provide optional local defaults without tracked paths.
SDK selection: -DotnetPath, SSC_DOTNET_PATH, the workspace portable SDK, then PATH.
#>
[CmdletBinding()]
param(
    [ValidateSet('1.6', '1.7')]
    [string]$GameVersion = '1.7',
    [string]$GameRoot = $env:SSC_GAME_ROOT,
    [string]$RjwAssemblyPath = $env:SSC_RJW_ASSEMBLY_PATH,
    [string]$AnimationsAssemblyPath = $env:SSC_ANIMATIONS_ASSEMBLY_PATH,
    [string]$GameHarmonyAssemblyPath = $env:SSC_GAME_HARMONY_PATH,
    [string]$DotnetPath = $env:SSC_DOTNET_PATH
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($GameVersion -ne '1.7') {
    throw '1.6 is the frozen 2.4.1 baseline and is no longer developed. This entry builds only 1.7.'
}

if (-not $DotnetPath) {
    $portableName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $portableSdk = Join-Path $repoRoot ".builds/version-layout/sdk/dotnet/$portableName"
    $DotnetPath = if (Test-Path -LiteralPath $portableSdk -PathType Leaf) { $portableSdk } else { 'dotnet' }
}
$dotnetCommand = Get-Command $DotnetPath -ErrorAction SilentlyContinue
if (-not $dotnetCommand) { throw "dotnet was not found: $DotnetPath. Supply -DotnetPath for a .NET 10 SDK installation." }
$dotnetExecutable = $dotnetCommand.Source
# --version obeys global.json and therefore checks the SDK this build will use,
# rather than merely checking whether a matching SDK appears in --list-sdks.
Push-Location $repoRoot
$savedSdkEnvironment = @{}
foreach ($name in @('DOTNET_CLI_HOME', 'DOTNET_GENERATE_ASPNET_CERTIFICATE', 'DOTNET_ADD_GLOBAL_TOOLS_TO_PATH')) {
    $savedSdkEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
    if (-not $env:DOTNET_CLI_HOME) {
        $env:DOTNET_CLI_HOME = Join-Path $repoRoot '.builds/dotnet-cli-home'
        [IO.Directory]::CreateDirectory($env:DOTNET_CLI_HOME) | Out-Null
    }
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    $sdkOutput = @(& $dotnetExecutable --version)
    if ($LASTEXITCODE -ne 0) { throw 'Could not determine the selected .NET SDK.' }
    $sdkVersion = ($sdkOutput -join "`n").Trim()
    if ($sdkVersion -notmatch '^(\d+)\.' -or [int]$Matches[1] -lt 10) {
        throw "RimWorld 1.7 requires a .NET 10 SDK for this project; selected SDK: $sdkVersion. Install a matching SDK or supply -DotnetPath. No baseline DLL was changed."
    }
    Write-Host "Using .NET SDK $sdkVersion ($dotnetExecutable)."

    if (-not $GameRoot) { throw 'Supply -GameRoot (or SSC_GAME_ROOT) for the target RimWorld 1.7 installation.' }
    $GameRoot = [IO.Path]::GetFullPath($GameRoot)
    $gameAssembly = Join-Path $GameRoot 'Assembly-CSharp.dll'
    foreach ($name in @('Assembly-CSharp.dll', 'UnityStubs.dll', 'Waylith.dll', 'Waylith.UnityCompat.dll', 'Brutal.Core.Maths.dll', 'Brutal.Core.Numerics.dll', 'Version.txt', 'RimWorld.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $GameRoot $name) -PathType Leaf)) {
            throw "The target 1.7 installation is missing $name. GameRoot: $GameRoot"
        }
    }
    $gameBuild = (Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim()
    if ($gameBuild -notmatch '^1\.7(?:\.|\s|$)') { throw "Expected RimWorld 1.7, found: $gameBuild" }
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $GameRoot 'RimWorld.runtimeconfig.json') -Raw | ConvertFrom-Json
    if ($runtimeConfig.runtimeOptions.tfm -ne 'net10.0') { throw 'The target 1.7 runtime configuration must declare net10.0.' }

    $referenceArguments = [ordered]@{
        RjwAssemblyPath = $RjwAssemblyPath
        AnimationsAssemblyPath = $AnimationsAssemblyPath
        GameHarmonyAssemblyPath = $GameHarmonyAssemblyPath
    }
    foreach ($name in @($referenceArguments.Keys)) {
        $value = $referenceArguments[$name]
        if (-not $value -or -not (Test-Path -LiteralPath $value -PathType Leaf)) {
            throw "Supply -$name with the DLL selected by the target 1.7 game environment."
        }
        $referenceArguments[$name] = [IO.Path]::GetFullPath($value)
    }

    $arguments = @('build', (Join-Path $repoRoot 'Sexslavecraft/SexSlaveCraft_1.7.csproj'), '--configuration', 'Release', '--nologo', "-p:GameRoot=$GameRoot")
    foreach ($name in $referenceArguments.Keys) { $arguments += "-p:$name=$($referenceArguments[$name])" }
    & $dotnetExecutable @arguments
    if ($LASTEXITCODE -ne 0) { throw "The 1.7 build failed (exit $LASTEXITCODE). BUILD-INFO.json was not replaced." }

    $assemblyPath = '1.7/Assemblies/Sexslavecraft.dll'
    $assemblyFullPath = Join-Path $repoRoot $assemblyPath
    if (-not (Test-Path -LiteralPath $assemblyFullPath -PathType Leaf)) { throw "Build output missing: $assemblyPath" }
    $version = (Get-Content -LiteralPath (Join-Path $repoRoot 'VERSION') -Raw).Trim()
    $fileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyFullPath)
    if ($fileInfo.FileVersion -ne "$version.0" -or $fileInfo.ProductVersion -ne $version) {
        throw 'The built DLL file/product version does not match VERSION; BUILD-INFO.json was not replaced.'
    }
    $assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assemblyFullPath)
    if ($assemblyName.Name -ne 'Sexslavecraft' -or $assemblyName.Version.ToString() -ne '1.0.0.0') {
        throw 'The built DLL changed the existing assembly identity; BUILD-INFO.json was not replaced.'
    }

    $sourceCommit = (& git -C $repoRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0) { throw 'Could not record the source Git commit for this build.' }
    $gitStatus = @(& git -C $repoRoot status --porcelain 2>$null)
    if ($LASTEXITCODE -ne 0) { throw 'Could not record the source Git working tree status.' }
    $references = foreach ($name in $referenceArguments.Keys) {
        [ordered]@{
            name = $name
            path = $referenceArguments[$name]
            sha256 = (Get-FileHash -LiteralPath $referenceArguments[$name] -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $buildInfo = [ordered]@{
        schemaVersion = 1
        gameVersion = $GameVersion
        binaryOrigin = 'rebuilt'
        sourceVersion = $version
        modVersion = $version
        sourceCommit = "$sourceCommit".Trim()
        sourceWorkingTreeDirty = ($gitStatus.Count -gt 0)
        assemblyPath = $assemblyPath
        sha256 = (Get-FileHash -LiteralPath $assemblyFullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        gameBuild = $gameBuild
        gameAssemblyPath = $gameAssembly
        gameAssemblySha256 = (Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
        sdkVersion = $sdkVersion
        references = @($references)
        gameRuntimeVerified = $false
    }
    $infoPath = Join-Path $repoRoot '1.7/BUILD-INFO.json'
    [IO.File]::WriteAllText($infoPath, (($buildInfo | ConvertTo-Json -Depth 6) + "`n"), [Text.UTF8Encoding]::new($false))
    Write-Host "Built $assemblyPath ($version); game runtime verification remains required."
}
finally {
    foreach ($name in $savedSdkEnvironment.Keys) {
        if ($null -eq $savedSdkEnvironment[$name]) {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($name, $savedSdkEnvironment[$name], 'Process')
        }
    }
    Pop-Location
}
