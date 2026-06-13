# ============================================================
# Culinaire Portal — Fix service binary path (run as Administrator)
# The original registration used a relative path. This corrects it.
# ============================================================

$exe = "C:\ClaudeOutput\Culinaire\publish\Portal.exe"

Write-Host "Updating service binary path to: $exe"
sc.exe config CulinairePortal binpath= $exe

Write-Host "Starting service..."
Start-Service -Name CulinairePortal

$status = (Get-Service -Name CulinairePortal).Status
Write-Host "Service status: $status"

if ($status -eq "Running") {
    Write-Host "Portal is running at: http://localhost:8080"
}
