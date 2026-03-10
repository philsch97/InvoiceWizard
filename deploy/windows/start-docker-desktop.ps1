$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$deployDir = Split-Path -Parent $scriptDir
$rootDir = Split-Path -Parent $deployDir
$composeFile = Join-Path $deployDir 'compose.server.yaml'
$envFile = Join-Path $deployDir '.env'
$envExample = Join-Path $deployDir '.env.example'

if (-not (Test-Path $envFile)) {
    Copy-Item $envExample $envFile
    Write-Host 'deploy/.env wurde aus .env.example erzeugt. Bitte Passwort und Ports bei Bedarf anpassen.'
}

Set-Location $rootDir

docker compose --env-file $envFile -f $composeFile up -d --build
docker compose --env-file $envFile -f $composeFile ps

Write-Host 'Docker-Desktop-Setup gestartet.'
Write-Host 'Backend: http://localhost:8080'
Write-Host 'Web:     http://localhost:8081'
Write-Host 'Falls WPF den Docker-Server nutzen soll:'
Write-Host '  $env:INVOICEWIZARD_API_BASEURL=''http://localhost:8080/'''
