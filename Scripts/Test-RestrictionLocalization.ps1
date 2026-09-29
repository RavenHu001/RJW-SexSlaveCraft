#requires -Version 7.0
<#
.SYNOPSIS
检查四种语言的行为限制文本完整性和格式参数。
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$languages = @('English', 'ChineseSimplified', 'ChineseTraditional', 'Russian')
$reference = $null

foreach ($language in $languages) {
    $path = Join-Path $repoRoot "Languages/$language/Keyed/SSC_Restrictions.xml"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing $language restriction translation: $path"
    }
    [xml]$document = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $entries = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::Ordinal)
    foreach ($node in $document.LanguageData.ChildNodes) {
        if ($node.NodeType -ne [Xml.XmlNodeType]::Element) { continue }
        if (-not $entries.TryAdd($node.Name, $node.InnerText)) {
            throw "Duplicate $language restriction key: $($node.Name)"
        }
    }
    if ($null -eq $reference) {
        $reference = $entries
        continue
    }
    if ($entries.Count -ne $reference.Count) {
        throw "$language has $($entries.Count) restriction keys; expected $($reference.Count)."
    }
    foreach ($key in $reference.Keys) {
        if (-not $entries.ContainsKey($key)) {
            throw "$language is missing restriction key $key."
        }
        $expected = @([regex]::Matches($reference[$key], '\{\d+\}') |
            ForEach-Object { $_.Value } | Sort-Object)
        $actual = @([regex]::Matches($entries[$key], '\{\d+\}') |
            ForEach-Object { $_.Value } | Sort-Object)
        if (($expected -join ',') -ne ($actual -join ',')) {
            throw "$language has mismatched placeholders for $key."
        }
    }
}

Write-Host "Restriction localization: $($reference.Count) keys verified in $($languages.Count) languages."
