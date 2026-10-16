#requires -Version 7.0
# Runtime paths are separate from the repository's source and test paths.
function Get-SscModLayout {
    [CmdletBinding()]
    param(
        [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
        [ValidateSet('1.6', '1.7')][string]$GameVersion = '1.7'
    )
    $root = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RepositoryRoot)
    [pscustomobject]@{
        RepositoryRoot = $root
        GameVersion = $GameVersion
        RuntimeRoot = Join-Path $root $GameVersion
        CommonRoot = Join-Path $root 'Common'
        AssemblyPath = Join-Path $root "$GameVersion/Assemblies/Sexslavecraft.dll"
    }
}
