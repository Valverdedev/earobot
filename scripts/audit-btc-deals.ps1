$json = Get-Content -Raw 'data\mt5\BTCUSDz_deals_auditoria.json' | ConvertFrom-Json
$deals = @($json.deals | Where-Object { $_.magicNumber -eq 828201 })
$groups = $deals | Group-Object positionId

$rows = New-Object System.Collections.Generic.List[object]
foreach ($group in $groups) {
    $arr = @($group.Group | Sort-Object timeUtc)
    $inDeal = $arr | Where-Object { $_.entry -eq 'DEAL_ENTRY_IN' } | Select-Object -First 1
    $outDeal = $arr | Where-Object { $_.entry -eq 'DEAL_ENTRY_OUT' -or $_.entry -eq 'DEAL_ENTRY_INOUT' } | Select-Object -Last 1

    $gross = [double](($arr | Measure-Object profit -Sum).Sum)
    $commission = [double](($arr | Measure-Object commission -Sum).Sum)
    $swap = [double](($arr | Measure-Object swap -Sum).Sum)
    $net = $gross + $commission + $swap
    $durationMin = $null

    if ($null -ne $inDeal -and $null -ne $outDeal) {
        $durationMin = [math]::Round((([datetime]$outDeal.timeUtc) - ([datetime]$inDeal.timeUtc)).TotalMinutes, 2)
    }

    $rows.Add([pscustomobject]@{
        PositionId = $group.Name
        Closed = $null -ne $outDeal
        Side = $(if ($null -ne $inDeal) { $inDeal.type -replace 'DEAL_TYPE_', '' } else { '' })
        EntryTime = $(if ($null -ne $inDeal) { $inDeal.timeUtc } else { $null })
        EntryPrice = $(if ($null -ne $inDeal) { [double]$inDeal.price } else { $null })
        ExitTime = $(if ($null -ne $outDeal) { $outDeal.timeUtc } else { $null })
        ExitPrice = $(if ($null -ne $outDeal) { [double]$outDeal.price } else { $null })
        Gross = [math]::Round($gross, 2)
        Commission = [math]::Round($commission, 2)
        Net = [math]::Round($net, 2)
        ExitComment = $(if ($null -ne $outDeal) { $outDeal.comment } else { '' })
        DurationMin = $durationMin
    })
}

$closed = @($rows | Where-Object { $_.Closed } | Sort-Object EntryTime)
$open = @($rows | Where-Object { -not $_.Closed })
$closed | ConvertTo-Json -Depth 4 | Set-Content 'data\mt5\BTCUSDz_closed_positions_828201.json' -Encoding UTF8

$grossTotal = [math]::Round((($closed | Measure-Object Gross -Sum).Sum), 2)
$commissionTotal = [math]::Round((($closed | Measure-Object Commission -Sum).Sum), 2)
$netTotal = [math]::Round((($closed | Measure-Object Net -Sum).Sum), 2)
$wins = @($closed | Where-Object { $_.Gross -gt 0 }).Count
$losses = @($closed | Where-Object { $_.Gross -lt 0 }).Count
$tp = @($closed | Where-Object { ([string]$_.ExitComment).StartsWith('[tp') }).Count
$sl = @($closed | Where-Object { ([string]$_.ExitComment).StartsWith('[sl') }).Count
$other = @($closed | Where-Object { -not ([string]$_.ExitComment).StartsWith('[tp') -and -not ([string]$_.ExitComment).StartsWith('[sl') }).Count

Write-Output "extractedAt=$($json.extractedAtUtc) totalDeals=$($json.deals.Count) strategyDeals=$($deals.Count)"
Write-Output "closedPositions=$($closed.Count) openWithoutExit=$($open.Count)"
Write-Output "gross=$grossTotal commission=$commissionTotal net=$netTotal wins=$wins losses=$losses tp=$tp sl=$sl other=$other"
if ($closed.Count -gt 0) {
    Write-Output "first=$($closed[0].EntryTime) last=$($closed[$closed.Count - 1].ExitTime)"
}
Write-Output "FIRST_5"
$closed | Select-Object -First 5 | Format-Table -AutoSize
Write-Output "LAST_5"
$closed | Select-Object -Last 5 | Format-Table -AutoSize
