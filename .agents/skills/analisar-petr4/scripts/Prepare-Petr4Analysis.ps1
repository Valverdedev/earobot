[CmdletBinding()]
param(
    [string]$ReportDirectory = "D:\SistemEarobot\relatorios\PETR4",
    [string]$ExtractorPath = "D:\SistemEarobot\financial.robot\publish-extractor\Financial.Robot.Extractor.exe",
    [string]$TerminalId = "genial",
    [string]$Symbol = "PETR4",
    [ValidateRange(105, 5000)]
    [int]$Count = 500,
    [ValidateRange(5, 300)]
    [int]$TimeoutSeconds = 30,
    [switch]$RunExtractor
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ReportDirectory)) {
    New-Item -ItemType Directory -Path $ReportDirectory -Force | Out-Null
}

$reportPattern = '^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_petr4\.md$'
$timestampPattern = 'yyyy-MM-dd_HH-mm-ss'
$culture = [System.Globalization.CultureInfo]::InvariantCulture

$latestReport = Get-ChildItem -LiteralPath $ReportDirectory -File |
    Where-Object { $_.Name -match $reportPattern } |
    ForEach-Object {
        $timestampText = $_.BaseName.Substring(0, 19)
        $parsedTimestamp = [DateTime]::MinValue
        $valid = [DateTime]::TryParseExact(
            $timestampText,
            $timestampPattern,
            $culture,
            [System.Globalization.DateTimeStyles]::None,
            [ref]$parsedTimestamp)
        if ($valid) {
            [PSCustomObject]@{ File = $_; Timestamp = $parsedTimestamp }
        }
    } |
    Sort-Object Timestamp -Descending |
    Select-Object -First 1

$preparedAt = Get-Date
$fileTimestamp = $preparedAt
do {
    $timestamp = $fileTimestamp.ToString($timestampPattern, $culture)
    $reportPath = Join-Path $ReportDirectory "${timestamp}_petr4.md"
    $extractionPath = Join-Path $ReportDirectory "${timestamp}_petr4_mt5.json"
    $candidatePath = Join-Path $ReportDirectory "${timestamp}_petr4_config.candidate.json"
    $paths = @($reportPath, $extractionPath, $candidatePath)
    $hasCollision = $paths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    $fileTimestamp = $fileTimestamp.AddSeconds(1)
} while ($hasCollision)

$extractorArguments = @(
    '--mode', 'extract',
    '--terminal', $TerminalId,
    '--symbol', $Symbol,
    '--kind', 'analysis',
    '--timeframes', 'M1,M5,M15,H1,H4,D1',
    '--count', $Count.ToString($culture),
    '--timeout', $TimeoutSeconds.ToString($culture),
    '--output', $extractionPath)

$result = $null
if ($RunExtractor) {
    if (-not (Test-Path -LiteralPath $ExtractorPath -PathType Leaf)) {
        throw "Extractor not found: $ExtractorPath"
    }

    try {
        & $ExtractorPath @extractorArguments
        if ($LASTEXITCODE -ne 0) {
            throw "Extractor failed with exit code $LASTEXITCODE."
        }
        if (-not (Test-Path -LiteralPath $extractionPath -PathType Leaf)) {
            throw "Extractor completed without creating: $extractionPath"
        }
        if ((Get-Item -LiteralPath $extractionPath).Length -eq 0) {
            throw "Extractor created an empty file: $extractionPath"
        }

        $result = [PSCustomObject]@{
            Label = "$TerminalId/$Symbol"
            Succeeded = $true
            Error = $null
        }
    }
    catch {
        $result = [PSCustomObject]@{
            Label = "$TerminalId/$Symbol"
            Succeeded = $false
            Error = $_.Exception.Message
        }
    }
}

[PSCustomObject]@{
    PreparedAt = $preparedAt.ToString('o', $culture)
    LatestReportPath = if ($latestReport) { $latestReport.File.FullName } else { $null }
    LatestReportTimestamp = if ($latestReport) { $latestReport.Timestamp.ToString('o', $culture) } else { $null }
    ReportPath = $reportPath
    ExtractionPath = $extractionPath
    CandidateConfigPath = $candidatePath
    ActiveConfigPath = 'D:\SistemEarobot\config\PETR4.config.json'
    ExtractionRequested = [bool]$RunExtractor
    AllExtractionsSucceeded = [bool]($RunExtractor -and $result -and $result.Succeeded)
    ExtractionResult = $result
    ExtractorPath = $ExtractorPath
    ExtractorArguments = $extractorArguments
}
