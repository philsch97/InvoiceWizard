param(
    [string]$ApiBaseUrl = 'http://localhost:5142/',
    [switch]$NewWindow
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$deployDir = Split-Path -Parent $scriptDir
$rootDir = Split-Path -Parent $deployDir
$wpfProject = Join-Path $rootDir 'InvoiceWizard\InvoiceWizard.csproj'
$backendProject = Join-Path $rootDir 'InvoiceWizard.Backend\InvoiceWizard.Backend.csproj'

function Test-BackendAvailable {
    param([string]$BaseUrl)

    $healthUrl = $BaseUrl.TrimEnd('/') + '/api/dashboard/summary'
    try {
        Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 2 | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Ensure-BackendRunning {
    param([string]$BaseUrl)

    if (Test-BackendAvailable -BaseUrl $BaseUrl) {
        Write-Host 'Backend ist bereits erreichbar.'
        return
    }

    if (-not $env:ConnectionStrings__PostgreSql) {
        $env:ConnectionStrings__PostgreSql = 'Host=localhost;Port=5432;Database=invoicewizard;Username=invoicewizard;Password=invoicewizard'
        Write-Host 'ConnectionStrings__PostgreSql wurde fuer den lokalen Standard gesetzt.'
    }

    $backendCommand = "Set-Location '$rootDir'; `$env:ConnectionStrings__PostgreSql='$($env:ConnectionStrings__PostgreSql)'; dotnet run --project '$backendProject'; if (`$LASTEXITCODE -ne 0) { Write-Host ''; Write-Host 'Das Backend wurde mit einem Fehler beendet.'; Read-Host 'Bitte Enter druecken' }"
    $backendProcess = Start-Process powershell -ArgumentList @('-NoExit', '-Command', $backendCommand) -PassThru
    Write-Host ('Backend wird gestartet: PID {0}' -f $backendProcess.Id)

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        if (Test-BackendAvailable -BaseUrl $BaseUrl) {
            Write-Host 'Backend ist jetzt erreichbar.'
            return
        }
    }

    throw 'Backend konnte nicht automatisch gestartet werden. Bitte das Backend-Fenster pruefen.'
}

Set-Location $rootDir
$env:INVOICEWIZARD_API_BASEURL = $ApiBaseUrl

Write-Host 'Starte InvoiceWizard WPF...'
Write-Host "API-Basis: $ApiBaseUrl"

Ensure-BackendRunning -BaseUrl $ApiBaseUrl

if ($NewWindow) {
    $wpfCommand = "Set-Location '$rootDir'; `$env:INVOICEWIZARD_API_BASEURL='$ApiBaseUrl'; dotnet run --project '$wpfProject'; if (`$LASTEXITCODE -ne 0) { Write-Host ''; Write-Host 'Die Anwendung wurde mit einem Fehler beendet.'; Read-Host 'Bitte Enter druecken' }"
    $process = Start-Process powershell -ArgumentList @('-NoExit', '-Command', $wpfCommand) -PassThru
    Write-Host ('WPF-Prozess gestartet: PID {0}' -f $process.Id)
    return
}

dotnet run --project $wpfProject
