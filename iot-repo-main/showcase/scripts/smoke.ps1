$ErrorActionPreference = 'Stop'
$base = 'http://localhost:8081'
$composeFile = Join-Path $PSScriptRoot '../compose.yaml'

function Assert-Check($condition, $message) {
    if (-not $condition) { throw $message }
}

function Run-Demo {
    $run = Invoke-RestMethod "$base/api/demo" -Method Post
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        Start-Sleep -Seconds 2
        $status = Invoke-RestMethod "$base/api/demo"
        if ($status.status -eq 'failed') { throw "Publisher failed: $($status.error)" }
        $allBags = Invoke-RestMethod "$base/api/luggage"
        $bags = @($allBags | Where-Object { $_.bagId -like "BAG-$($run.demoId)-*" })
    } while (($status.status -ne 'completed' -or $bags.Count -ne 3 -or @($bags | Where-Object trackingPointId -ne 'sorting').Count -gt 0) -and [DateTime]::UtcNow -lt $deadline)
    Assert-Check ($status.status -eq 'completed' -and $bags.Count -eq 3) 'Demo did not complete with three bags'
    foreach ($bag in $bags) {
        Assert-Check ($bag.trackingPointId -eq 'sorting') 'Bag did not finish at sorting'
        $details = Invoke-RestMethod "$base/api/luggage/$($bag.bagId)"
        Assert-Check ($details.observations.Count -eq 3) 'Expected exactly three stored observations per bag'
        Assert-Check (($details.observations.trackingPointId -join ',') -eq 'checkin,security,sorting') 'Unexpected tracking point order'
    }
    return $bags
}

$health = Invoke-RestMethod "$base/health"
Assert-Check ($health.database -and $health.mqtt) 'Stack is not ready'
$first = @(Run-Demo)
docker compose -f $composeFile restart
if ($LASTEXITCODE -ne 0) { throw 'Compose restart failed' }
$ready = $false
$deadline = [DateTime]::UtcNow.AddSeconds(60)
do {
    Start-Sleep -Seconds 2
    try { $health = Invoke-RestMethod "$base/health"; $ready = $health.database -and $health.mqtt } catch { $ready = $false }
} while (-not $ready -and [DateTime]::UtcNow -lt $deadline)
Assert-Check $ready 'Stack did not recover after restart'
Assert-Check ((Invoke-RestMethod "$base/api/demo").status -eq 'idle') 'Demo should not resume after restart'
foreach ($bag in $first) {
    $details = Invoke-RestMethod "$base/api/luggage/$($bag.bagId)"
    Assert-Check ($details.observations.Count -eq 3 -and $details.current.trackingPointId -eq 'sorting') 'History did not survive restart'
}
$second = @(Run-Demo)
Assert-Check (@($second | Where-Object { $_.bagId -in $first.bagId }).Count -eq 0) 'Demo reused bag IDs'
foreach ($bag in $first) {
    Assert-Check ((Invoke-RestMethod "$base/api/luggage/$($bag.bagId)").observations.Count -eq 3) 'Second demo altered earlier history'
}
Write-Host 'PASS: 18 observations across two distinct demos; history survived stack restart.'
