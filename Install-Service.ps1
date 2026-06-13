# ============================================================
# Culinaire Portal — Windows Service Install Script
# Run as Administrator in PowerShell
# ============================================================

param(
    [string]$PublishPath = "C:\ClaudeOutput\Culinaire\publish",
    [string]$ServiceName = "CulinairePortal",
    [string]$DisplayName = "Culinaire Portal",
    [string]$Description = "Culinaire Portal Web Application (Blazor Server on Kestrel)"
)

# Always resolve to an absolute path so the service binary path is never relative.
$PublishPath = (Resolve-Path $PublishPath).Path
$exe         = Join-Path $PublishPath "Portal.exe"

if (-not (Test-Path $exe)) {
    Write-Error "Published executable not found at: $exe"
    Write-Host "Run: dotnet publish Portal -c Release -o `"$PublishPath`""
    exit 1
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Stopping existing service..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating Windows Service: $ServiceName"
Write-Host "Binary: $exe"

New-Service -Name $ServiceName `
            -BinaryPathName $exe `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic

Write-Host "Starting service..."
Start-Service -Name $ServiceName

$status = (Get-Service -Name $ServiceName).Status
Write-Host "Service status: $status"
Write-Host ""
Write-Host "Portal available at: http://localhost:8080"
