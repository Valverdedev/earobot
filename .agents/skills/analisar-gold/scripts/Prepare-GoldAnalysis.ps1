[CmdletBinding()]
param(
    [string]$ReportDirectory = "D:\SistemEarobot\relatorios\GOLD",
    [string]$ExtractorPath = "D:\SistemEarobot\financial.robot\publish-extractor\Financial.Robot.Extractor.exe",
    [string]$TerminalId = "activtraders",
    [string]$Symbol = "GOLD",
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

$reportPattern = '^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_gold\.md$'
$timestampPattern = 'yyyy-MM-dd_HH-mm-ss'
$culture = [System.Globalization.CultureInfo]::InvariantCulture

$latestReport = Get-ChildItem -LiteralPath $ReportDirectory -File |
    Where-Object { $_.Name -match $reportPattern } |
    ForEach-Object {
        $timestampText = $_.BaseName.Substring(0, 19)
        $parsedTimestamp = [DateTime]::MinValue
        $isValidTimestamp = [DateTime]::TryParseExact(
            $timestampText,
            $timestampPattern,
            $culture,
            [System.Globalization.DateTimeStyles]::None,
            [ref]$parsedTimestamp)

        if ($isValidTimestamp) {
            [PSCustomObject]@{
                File = $_
                Timestamp = $parsedTimestamp
            }
        }
    } |
    Sort-Object Timestamp -Descending |
    Select-Object -First 1

$preparedAt = Get-Date
$fileTimestamp = $preparedAt
do {
    $timestamp = $fileTimestamp.ToString($timestampPattern, $culture)
    $reportPath = Join-Path $ReportDirectory "${timestamp}_gold.md"
    $extractionPath = Join-Path $ReportDirectory "${timestamp}_gold_mt5.json"
    $candidateConfigPath = Join-Path $ReportDirectory "${timestamp}_gold_config.candidate.json"
    $hasCollision = (Test-Path -LiteralPath $reportPath) -or
        (Test-Path -LiteralPath $extractionPath) -or
        (Test-Path -LiteralPath $candidateConfigPath)
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
    '--output', $extractionPath
)

$extractionRan = $false
if ($RunExtractor) {
    if (-not (Test-Path -LiteralPath $ExtractorPath -PathType Leaf)) {
        throw "Extractor not found: $ExtractorPath"
    }

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

    $extractionRan = $true
}

[PSCustomObject]@{
    PreparedAt = $preparedAt.ToString('o', $culture)
    LatestReportPath = if ($latestReport) { $latestReport.File.FullName } else { $null }
    LatestReportTimestamp = if ($latestReport) { $latestReport.Timestamp.ToString('o', $culture) } else { $null }
    ReportPath = $reportPath
    ExtractionPath = $extractionPath
    CandidateConfigPath = $candidateConfigPath
    ActiveConfigPath = 'D:\SistemEarobot\config\GOLD.config.json'
    ExtractionRan = $extractionRan
    ExtractorPath = $ExtractorPath
    ExtractorArguments = $extractorArguments
}
