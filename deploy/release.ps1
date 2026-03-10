$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $scriptDir
$composeFile = Join-Path $scriptDir 'compose.server.yaml'
$envFile = Join-Path $scriptDir '.env'

if (-not (Test-Path $envFile)) {
    Write-Error 'deploy/.env fehlt. Bitte zuerst deploy/.env.example nach deploy/.env kopieren und anpassen.'
}

Set-Location $rootDir

Write-Host '[1/4] Images bauen und Container aktualisieren'
docker compose --env-file $envFile -f $composeFile up -d --build

Write-Host '[2/4] Laufende Container anzeigen'
docker compose --env-file $envFile -f $composeFile ps

Write-Host '[3/4] Letzte Backend-Logs'
docker compose --env-file $envFile -f $composeFile logs backend --tail=50

Write-Host '[4/4] Deployment abgeschlossen'
Write-Host 'Web:     http://localhost:8081'
Write-Host 'Backend: http://localhost:8080'
