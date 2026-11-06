# Records all USB traffic while Castle Link 2 reads the tune off the ESC.
# Run through "Capture Castle tune.bat", which gets admin rights first.

$usbPcap = 'C:\Program Files\USBPcap\USBPcapCMD.exe'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$runFolder = Join-Path $repoRoot ("Tests\castle-link-captures\tune_" + (Get-Date -Format 'yyyy-MM-dd_HHmmss'))

if (-not (Test-Path $usbPcap)) {
    Write-Host 'USBPcap is not installed, so nothing was captured.' -ForegroundColor Red
    return
}

if (Get-Process | Where-Object { $_.ProcessName -match 'Castle' }) {
    Write-Host 'Close the Castle app, then run this again.' -ForegroundColor Red
    Write-Host 'The recording has to start before the app first talks to the Castle Link.'
    return
}

New-Item -ItemType Directory -Force $runFolder | Out-Null

# USBPcap has one channel per USB root hub. Channels that do not exist exit at once.
$captures = foreach ($channel in 1..6) {
    $file = Join-Path $runFolder "usb-channel-$channel.pcap"
    $process = Start-Process -FilePath $usbPcap -WindowStyle Hidden -PassThru `
        -ArgumentList "-d \\.\USBPcap$channel -A -o `"$file`""
    [pscustomobject]@{ Channel = $channel; File = $file; Process = $process }
}

Start-Sleep -Seconds 3
$running = @($captures | Where-Object { -not $_.Process.HasExited })
$captures | Where-Object { $_.Process.HasExited } | ForEach-Object {
    Remove-Item $_.File -ErrorAction SilentlyContinue
}

if ($running.Count -eq 0) {
    Write-Host 'Recording did not start on any USB channel, so nothing was captured.' -ForegroundColor Red
    return
}

Write-Host ''
Write-Host 'RECORDING NOW. Leave this window open.' -ForegroundColor Green
Write-Host ''
Write-Host '1. Open Castle Link 2 and connect to the ESC.'
Write-Host '2. Read the settings off the ESC, the same way you normally do.'
Write-Host '3. Save the tune as a .dat file in this folder:'
Write-Host "   $runFolder" -ForegroundColor Yellow
Write-Host '4. Close Castle Link 2.'
Write-Host ''
Read-Host 'When all of that is done, press Enter here to stop recording'

# Give USBPcap a moment to write out the last of the data before stopping it.
Start-Sleep -Seconds 3
$running | ForEach-Object { Stop-Process -Id $_.Process.Id -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 1

Write-Host ''
Write-Host 'Recording stopped. Saved:' -ForegroundColor Green
Get-ChildItem $runFolder -File | ForEach-Object {
    Write-Host ("  {0}  ({1:N0} KB)" -f $_.Name, ($_.Length / 1KB))
}
Write-Host ''
Write-Host 'Tell Claude it is done. You can close this window.'
