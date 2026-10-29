#requires -Version 7.0
<#
.SYNOPSIS
检查受影响Keyed文件的四语结构、消费键和格式参数，不加载游戏程序集。
.DESCRIPTION
默认只检查本轮精简涉及的六个文件。五个模块文件以English核对完整键集合及参数索引；
混合模块SSC_Keys.xml只对MilkProductionToggle消费合同做跨语对齐，其余键仍检查结构和格式合法。
独立消费合同防止四语同时漏键。允许调整正文、参数次序和格式，不固定键总数。
#>
[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [string]$RuntimeRoot,
    [ValidateNotNullOrEmpty()][string[]]$Files = @(
        'SSC_RomanceMood.xml', 'SSC_Romance.xml', 'SSC_BodyManagement.xml',
        'SSC_UterusDevelopment.xml', 'SSC_Cosmetic.xml', 'SSC_Keys.xml'
    ),
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
if (-not $RuntimeRoot) { $RuntimeRoot = Join-Path $repo '1.7' }
$runtime = (Resolve-Path -LiteralPath $RuntimeRoot).Path
$languages = @('English', 'ChineseSimplified', 'ChineseTraditional', 'Russian')
$contractOnlyFiles = @('SSC_Keys.xml')
$selected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $Files) {
    if ([IO.Path]::GetFileName($file) -cne $file -or [IO.Path]::GetExtension($file) -cne '.xml') {
        throw "Expected a Keyed XML filename, got: $file"
    }
    if (-not $selected.Add($file)) { throw "Duplicate selected filename: $file" }
}

function Get-FormatIndices([string]$value, [string]$context) {
    $indices = [Collections.Generic.HashSet[int]]::new()
    $normalized = [Text.StringBuilder]::new()
    for ($offset = 0; $offset -lt $value.Length; $offset++) {
        $character = $value[$offset]
        if ($character -eq '{' -and $offset + 1 -lt $value.Length -and $value[$offset + 1] -eq '{') {
            [void]$normalized.Append('{{'); $offset++; continue
        }
        if ($character -ne '{') { [void]$normalized.Append($character); continue }
        $start = ++$offset
        while ($offset -lt $value.Length -and $value[$offset] -ge '0' -and $value[$offset] -le '9') { $offset++ }
        $index = 0
        if ($start -eq $offset -or -not [int]::TryParse($value.Substring($start, $offset - $start), [ref]$index)) {
            throw "Invalid numeric format item in ${context}: $value"
        }
        [void]$indices.Add($index)
        # 只归零参数索引，保留原括号／对齐／format交给运行时解析，不按异常索引分配巨大数组。
        [void]$normalized.Append('{0'); $offset--
    }
    try { $null = [string]::Format([Globalization.CultureInfo]::InvariantCulture, $normalized.ToString(), [object[]]@('')) }
    catch { throw "Invalid composite format in ${context}: $($_.Exception.Message)" }
    return [string]::Join(',', [string[]]@($indices | Sort-Object))
}

function Read-KeyedEntries([string]$path, [string]$context) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing Keyed file: $path" }
    $settings = [Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($path, $settings)
    $document = [Xml.XmlDocument]::new()
    try { $document.Load($reader) }
    finally { $reader.Dispose() }
    if ($document.DocumentElement.Name -cne 'LanguageData' -or $document.DocumentElement.NamespaceURI) {
        throw "Expected an unnamespaced LanguageData root in $context"
    }
    $entries = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($node in $document.DocumentElement.ChildNodes) {
        if ($node.NodeType -eq [Xml.XmlNodeType]::Comment -or
            $node.NodeType -eq [Xml.XmlNodeType]::Whitespace -or
            $node.NodeType -eq [Xml.XmlNodeType]::SignificantWhitespace) { continue }
        if ($node.NodeType -ne [Xml.XmlNodeType]::Element -or $node.NamespaceURI -or $node.SelectNodes('*').Count) {
            throw "Expected a plain text Keyed entry in $context"
        }
        if ([string]::IsNullOrWhiteSpace($node.InnerText)) { throw "Empty Keyed text in ${context}: $($node.Name)" }
        if (-not $entries.TryAdd($node.Name, $node.InnerText)) { throw "Duplicate Keyed entry in ${context}: $($node.Name)" }
    }
    if (-not $entries.Count) { throw "No Keyed entries in $context" }
    return ,$entries
}

# 仅记录本轮移除语言矩阵实际消费的接口；不复制正文，也不固定资源文件的键总数。
$contracts = @{
    # 孕前菜单包含拼接键；独立列出消费接口，避免四语同时缺少同一选项仍通过对齐检查。
    'SSC_FertilityPreconception.xml' = @{
        'SSC_Preconception_GenderLabel' = ''; 'SSC_Preconception_SpeciesLabel' = ''; 'SSC_Preconception_ImprintLabel' = ''
        'SSC_Preconception_GenderNatural' = ''; 'SSC_Preconception_GenderMale' = ''; 'SSC_Preconception_GenderFemale' = ''
        'SSC_Preconception_SpeciesNatural' = ''; 'SSC_Preconception_SpeciesSelf' = ''; 'SSC_Preconception_SpeciesPartner' = ''
        'SSC_Preconception_GenderTip' = ''; 'SSC_Preconception_SpeciesTip' = ''; 'SSC_Preconception_ImprintTip' = ''
        'SSC_Preconception_GenderLocked' = ''; 'SSC_Preconception_SpeciesLocked' = ''; 'SSC_Preconception_ImprintLocked' = ''
        'SSC_Preconception_PregnancyBlocked' = ''; 'SSC_Preconception_FertilityDisabled' = ''
        'SSC_Preconception_Unavailable' = ''; 'SSC_Preconception_InvalidSelection' = ''
        'SSC_Preconception_DirectionRequired' = ''
        'SSC_Preconception_MissingUterus' = ''
    }
    'SSC_FertilitySpecialization.xml' = @{
        'SSC_Fertility_InactiveLabel' = ''; 'SSC_Fertility_InactiveDescription' = '0'
    }
    'SSC_Imprint.xml' = @{
        'SSC_Imprint_None' = ''; 'SSC_Imprint_Unlocked' = ''; 'SSC_Imprint_MasterLocked' = ''; 'SSC_Imprint_SlaveLocked' = ''
        'SSC_Imprint_GelStatus' = '0'; 'SSC_Imprint_GelTooltip' = ''; 'SSC_Imprint_SeedAlreadyMarked' = ''
        'SSC_Imprint_SeedIdentityBlocked' = ''; 'SSC_Imprint_SeedUsed' = '0'
    }
    'SSC_BodyManagement.xml' = @{
        'SSC_BodyManagement_Title' = ''; 'SSC_BodyManagement_FertilityToggle' = ''; 'SSC_BodyManagement_NotStarted' = ''
    }
    'SSC_UterusDevelopment.xml' = @{
        'SSC_UterusDevelopment_CannotStart' = ''; 'SSC_UterusDevelopment_AlreadyStarted' = ''
        'SSC_UterusDevelopment_MissingTorso' = ''; 'SSC_UterusDevelopment_ConfigUnavailable' = ''
        'SSC_UterusDevelopment_SlaveIdentityRequired' = ''; 'SSC_UterusDevelopment_PregnancyBlocked' = ''
        'SSC_UterusDevelopment_MissingOrgan' = ''; 'SSC_UterusDevelopment_AdultFemaleRequired' = ''
        'SSC_UterusDevelopment_DoctorRequired' = ''; 'SSC_UterusDevelopment_Started' = '0'
    }
    'SSC_Cosmetic.xml' = @{ 'SSC_Cosmetic_MedicineRequired' = '0' }
    'SSC_Keys.xml' = @{ 'SSC_ITab_MilkProductionToggle' = '' }
}
if ($selected.Contains('SSC_RomanceMood.xml')) {
    $contractPath = Join-Path $repo 'Docs/Development/Reference/性奴浪漫综合心情完整分支表.md'
    $document = Get-Content -LiteralPath $contractPath -Raw -Encoding UTF8
    $moodContract = @{'SSC_RomanceMood_Unknown_Name' = ''; 'SSC_RomanceMood_Unknown_Description' = ''}
    foreach ($section in @(
        '(?ms)^## 2\. 完整事件分支\r?\n(?<rows>.*?)(?=^## 3\.)',
        '(?ms)^## 4\. 持续思想分支补全\r?\n(?<rows>.*?)(?=^## 5\.)'
    )) {
        $rows = [regex]::Match($document, $section)
        if (-not $rows.Success) { throw "Missing independent mood contract section in $contractPath" }
        $ids = [regex]::Matches($rows.Groups['rows'].Value, '(?m)^\|\s*`(?<id>E\d{2}-S[1-4]-[NM]-[DGC]|C-[A-Z]+-S[1-4]-[NM])`\s*\|')
        if (-not $ids.Count) { throw "No independent mood branch IDs in $contractPath" }
        foreach ($id in $ids) {
            $prefix = 'SSC_RomanceMood_' + $id.Groups['id'].Value.Replace('-', '_')
            foreach ($suffix in @('Name', 'Description')) { $moodContract["${prefix}_$suffix"] = '' }
        }
    }
    $contracts['SSC_RomanceMood.xml'] = $moodContract
}

$seen = @{}
foreach ($language in $languages) { $seen[$language] = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal) }
$results = [Collections.Generic.List[object]]::new()
foreach ($file in $Files) {
    $reference = $null
    $referenceFormats = @{}
    foreach ($language in $languages) {
        $context = "$language/$file"
        $entries = Read-KeyedEntries (Join-Path $runtime "Languages/$language/Keyed/$file") $context
        $formats = @{}
        foreach ($key in $entries.Keys) {
            if (-not $seen[$language].Add($key)) { throw "Duplicate Keyed entry across selected files in ${context}: $key" }
            $formats[$key] = Get-FormatIndices $entries[$key] "$context/$key"
        }
        if ($contracts.ContainsKey($file)) {
            foreach ($key in $contracts[$file].Keys) {
                if (-not $entries.ContainsKey($key)) { throw "Missing consumed Keyed entry in ${context}: $key" }
                if ($formats[$key] -cne $contracts[$file][$key]) { throw "Consumed parameter contract mismatch in ${context}: $key" }
            }
        }
        if ($null -eq $reference) { $reference = $entries; $referenceFormats = $formats }
        else {
            $alignedKeys = if ($file -in $contractOnlyFiles) { $contracts[$file].Keys } else { $reference.Keys }
            foreach ($key in $alignedKeys) {
                if (-not $entries.ContainsKey($key)) { throw "Missing English-reference Keyed entry in ${context}: $key" }
                if ($formats[$key] -cne $referenceFormats[$key]) { throw "Parameter indices differ from English in ${context}: $key" }
            }
            if ($file -notin $contractOnlyFiles) {
                foreach ($key in $entries.Keys) {
                    if (-not $reference.ContainsKey($key)) { throw "Unexpected Keyed entry absent from English in ${context}: $key" }
                }
            }
        }
        $alignedCount = if ($file -in $contractOnlyFiles) { $contracts[$file].Count } else { $entries.Count }
        $results.Add([pscustomobject]@{ Language = $language; File = $file; ParsedKeys = $entries.Count; AlignedKeys = $alignedCount })
    }
}
$report = [pscustomobject]@{
    Success = $true; RuntimeRoot = $runtime; Files = $Files; Languages = $languages
    ContractOnlyFiles = @($Files | Where-Object { $_ -in $contractOnlyFiles })
    XmlFiles = $results.Count; ParsedKeyEntries = ($results | Measure-Object ParsedKeys -Sum).Sum
    AlignedKeyEntries = ($results | Measure-Object AlignedKeys -Sum).Sum; Details = $results.ToArray()
}
Write-Host "Keyed localization: $($report.XmlFiles) XML files parsed / $($report.ParsedKeyEntries) entries checked / $($report.AlignedKeyEntries) entries aligned across languages; no game assemblies loaded."
if ($PassThru) { $report }
