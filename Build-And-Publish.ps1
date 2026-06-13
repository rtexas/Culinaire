#Requires -RunAsAdministrator
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$svc    = 'CulinairePortal'
$srcDir = "$PSScriptRoot\Portal"
$pubDir = "$PSScriptRoot\publish"

Write-Host "Stopping service..." -ForegroundColor Cyan
$s = Get-Service $svc -ErrorAction SilentlyContinue
if ($s -and $s.Status -eq 'Running') {
    Stop-Service $svc
    $s.WaitForStatus('Stopped', '00:00:30')
    Write-Host "  Stopped." -ForegroundColor Green
} else {
    Write-Host "  Not running — skipping stop." -ForegroundColor Yellow
}

Write-Host "Publishing..." -ForegroundColor Cyan
dotnet publish "$srcDir\Portal.csproj" -c Release -r win-x64 --self-contained true -o $pubDir
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed."; exit 1 }
Write-Host "  Published to: $pubDir" -ForegroundColor Green

Write-Host "Starting service..." -ForegroundColor Cyan
Start-Service $svc
(Get-Service $svc).WaitForStatus('Running', '00:00:30')
Write-Host "  Running." -ForegroundColor Green

Write-Host ""
Write-Host "Done. Browse to http://localhost:8080/login" -ForegroundColor Cyan
