# Shares the app on the internet through a Microsoft dev tunnel, for Start.bat.
# The tunnel is created once (labelled "comptracker") and reused, so the link stays the same between runs.
# It opens the tunnel host in its own window and returns; close that window to take the link offline.
param([int]$Port = 5080)

$ErrorActionPreference = 'Stop'
$Label = 'comptracker'

function Find-DevTunnel {
    $cmd = Get-Command devtunnel -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $installed = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet" -Recurse -Filter devtunnel.exe -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($installed) { return $installed.FullName }
    return $null
}

$dt = Find-DevTunnel
if (-not $dt) {
    Write-Host 'Installing the dev tunnels CLI...'
    winget install --id Microsoft.devtunnel -e --accept-source-agreements --accept-package-agreements | Out-Host
    $dt = Find-DevTunnel
    if (-not $dt) { Write-Host 'Could not install devtunnel. Skipping the online link.'; exit 1 }
}

if ((& $dt user show -j | ConvertFrom-Json).status -ne 'Logged in') {
    Write-Host 'Sign in to dev tunnels (a Microsoft or GitHub account) in the browser window that opens.'
    & $dt user login | Out-Host
    if ((& $dt user show -j | ConvertFrom-Json).status -ne 'Logged in') { Write-Host 'Not signed in. Skipping the online link.'; exit 1 }
}

$tunnel = (& $dt list --labels $Label -j | ConvertFrom-Json).tunnels | Select-Object -First 1
if ($tunnel) {
    $id = $tunnel.tunnelId
    # Unused tunnels expire after 30 days; push that back each time the app starts.
    & $dt update $id -e 30d -j | Out-Null
} else {
    Write-Host 'Creating the tunnel (one time)...'
    $id = (& $dt create --allow-anonymous -l $Label -d 'Comp Tracker' -e 30d -j | ConvertFrom-Json).tunnel.tunnelId
}

$ports = (& $dt show $id -j | ConvertFrom-Json).tunnel.ports
if (-not ($ports | Where-Object portNumber -eq $Port)) {
    & $dt port create $id -p $Port -j | Out-Null
    $ports = (& $dt show $id -j | ConvertFrom-Json).tunnel.ports
}
$url = ($ports | Where-Object portNumber -eq $Port).portUri

if ((& $dt show $id -j | ConvertFrom-Json).tunnel.hostConnections -gt 0) {
    Write-Host "The tunnel is already running elsewhere."
} else {
    Start-Process -FilePath $dt -ArgumentList 'host', $id -WindowStyle Minimized
}

Write-Host ''
Write-Host "  Online link: $url"
Write-Host '  Anyone with it can view the dashboard; admin pages still need the PIN.'
