$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$deployDir = Split-Path -Parent $scriptDir
$rootDir = Split-Path -Parent $deployDir
$backendProject = Join-Path $rootDir 'InvoiceWizard.Backend\InvoiceWizard.Backend.csproj'
$webProject = Join-Path $rootDir 'InvoiceWizard.Web\InvoiceWizard.Web.csproj'
$backendCommand = "Set-Location '$rootDir'; dotnet run --project '$backendProject'"
$webCommand = "Set-Location '$rootDir'; dotnet run --project '$webProject'"

Set-Location $rootDir

Write-Host 'Starte Backend und Web-App fuer lokalen Windows-Betrieb...'
Write-Host 'PostgreSQL muss separat laufen, zum Beispiel per docker compose up -d'

$backend = Start-Process powershell -ArgumentList @('-NoExit', '-Command', $backendCommand) -PassThru
$web = Start-Process powershell -ArgumentList @('-NoExit', '-Command', $webCommand) -PassThru

Write-Host ('Backend-Prozess gestartet: PID {0}' -f $backend.Id)
Write-Host ('Web-Prozess gestartet:     PID {0}' -f $web.Id)
Write-Host 'Backend lokal: http://localhost:5142'
Write-Host 'Web lokal:     http://localhost:5286'
Write-Host 'WPF nutzt standardmaessig bereits http://localhost:5142/'
