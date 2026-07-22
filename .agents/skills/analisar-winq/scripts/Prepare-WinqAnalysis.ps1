[CmdletBinding()]
param(
    [string]$ReportDirectory = "D:\SistemEarobot\relatorios\WINQ",
    [string]$ExtractorPath = "D:\SistemEarobot\financial.robot\publish-extractor\Financial.Robot.Extractor.exe",
    [string]$PrimaryTerminalId = "genial",
    [string]$PrimarySymbol = "WINQ26",
    [string]$SecondaryTerminalId = "activtraders",
    [string]$SecondarySymbol = "Bra50Aug26",
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

$reportPattern = '^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}_winq\.md$'
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
    $reportPath = Join-Path $ReportDirectory "${timestamp}_winq.md"
    $primaryExtractionPath = Join-Path $ReportDirectory "${timestamp}_winq26_mt5.json"
    $secondaryExtractionPath = Join-Path $ReportDirectory "${timestamp}_bra50aug26_mt5.json"
    $primaryCandidatePath = Join-Path $ReportDirectory "${timestamp}_winq26_config.candidate.json"
    $secondaryCandidatePath = Join-Path $ReportDirectory "${timestamp}_bra50aug26_config.candidate.json"
    $paths = @(
        $reportPath,
        $primaryExtractionPath,
        $secondaryExtractionPath,
        $primaryCandidatePath,
        $secondaryCandidatePath)
    $hasCollision = $paths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    $fileTimestamp = $fileTimestamp.AddSeconds(1)
} while ($hasCollision)

function New-ExtractorArguments {
    param(
        [string]$TerminalId,
        [string]$Symbol,
        [string]$OutputPath
    )

    return @(
        '--mode', 'extract',
        '--terminal', $TerminalId,
        '--symbol', $Symbol,
        '--kind', 'analysis',
        '--timeframes', 'M1,M5,M15,H1,H4,D1',
        '--count', $Count.ToString($culture),
        '--timeout', $TimeoutSeconds.ToString($culture),
        '--output', $OutputPath)
}

function Invoke-Extraction {
    param(
        [string]$Label,
        [string[]]$Arguments,
        [string]$OutputPath
    )

    try {
        & $ExtractorPath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Extractor failed with exit code $LASTEXITCODE."
        }
        if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
            throw "Extractor completed without creating: $OutputPath"
        }
        if ((Get-Item -LiteralPath $OutputPath).Length -eq 0) {
            throw "Extractor created an empty file: $OutputPath"
        }

        return [PSCustomObject]@{
            Label = $Label
            Succeeded = $true
            Error = $null
        }
    }
    catch {
        return [PSCustomObject]@{
            Label = $Label
            Succeeded = $false
            Error = $_.Exception.Message
        }
    }
}

$primaryArguments = New-ExtractorArguments -TerminalId $PrimaryTerminalId -Symbol $PrimarySymbol -OutputPath $primaryExtractionPath
$secondaryArguments = New-ExtractorArguments -TerminalId $SecondaryTerminalId -Symbol $SecondarySymbol -OutputPath $secondaryExtractionPath
$results = @()

if ($RunExtractor) {
    if (-not (Test-Path -LiteralPath $ExtractorPath -PathType Leaf)) {
        throw "Extractor not found: $ExtractorPath"
    }

    $results += Invoke-Extraction -Label "$PrimaryTerminalId/$PrimarySymbol" -Arguments $primaryArguments -OutputPath $primaryExtractionPath
    $results += Invoke-Extraction -Label "$SecondaryTerminalId/$SecondarySymbol" -Arguments $secondaryArguments -OutputPath $secondaryExtractionPath
}

$allSucceeded = $RunExtractor -and
    ($results.Count -eq 2) -and
    -not ($results | Where-Object { -not $_.Succeeded })

[PSCustomObject]@{
    PreparedAt = $preparedAt.ToString('o', $culture)
    LatestReportPath = if ($latestReport) { $latestReport.File.FullName } else { $null }
    LatestReportTimestamp = if ($latestReport) { $latestReport.Timestamp.ToString('o', $culture) } else { $null }
    ReportPath = $reportPath
    PrimaryExtractionPath = $primaryExtractionPath
    SecondaryExtractionPath = $secondaryExtractionPath
    PrimaryCandidateConfigPath = $primaryCandidatePath
    SecondaryCandidateConfigPath = $secondaryCandidatePath
    PrimaryActiveConfigPath = 'D:\SistemEarobot\config\WINQ26.config.json'
    SecondaryActiveConfigPath = 'D:\SistemEarobot\config\Bra50Aug26.config.json'
    ExtractionRequested = [bool]$RunExtractor
    AllExtractionsSucceeded = $allSucceeded
    ExtractionResults = $results
    ExtractorPath = $ExtractorPath
    PrimaryExtractorArguments = $primaryArguments
    SecondaryExtractorArguments = $secondaryArguments
}
