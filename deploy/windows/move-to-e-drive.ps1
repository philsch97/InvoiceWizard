param(
    [string]$RuntimeRoot = 'E:\InvoiceWizard'
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$deployDir = Split-Path -Parent $scriptDir
$rootDir = Split-Path -Parent $deployDir

$runtimeDir = Join-Path $RuntimeRoot 'runtime'
$backendOut = Join-Path $runtimeDir 'backend'
$wpfOut = Join-Path $runtimeDir 'wpf'
$dataDir = Join-Path $RuntimeRoot 'data'
$postgresDir = Join-Path $dataDir 'postgres'
$launcherPath = Join-Path $RuntimeRoot 'start-invoicewizard.ps1'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('Desktop')) 'InvoiceWizard.lnk'
$powerShellExe = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'

New-Item -ItemType Directory -Force -Path $backendOut | Out-Null
New-Item -ItemType Directory -Force -Path $wpfOut | Out-Null
New-Item -ItemType Directory -Force -Path $postgresDir | Out-Null

Set-Location $rootDir

dotnet publish .\InvoiceWizard.Backend\InvoiceWizard.Backend.csproj -c Release -o $backendOut
dotnet publish .\InvoiceWizard\InvoiceWizard.csproj -c Release -o $wpfOut

$launcherContent = @"
param()

`$ErrorActionPreference = 'Stop'
`$runtimeRoot = '$RuntimeRoot'
`$repoRoot = '$rootDir'
`$postgresData = Join-Path `$runtimeRoot 'data\postgres'
`$backendDll = Join-Path `$runtimeRoot 'runtime\backend\InvoiceWizard.Backend.dll'
`$wpfExe = Join-Path `$runtimeRoot 'runtime\wpf\InvoiceWizard.exe'
`$backendLog = Join-Path `$runtimeRoot 'backend-start.log'
`$powerShellExe = '$powerShellExe'

function Write-Step {
    param([string]`$Message)
    Write-Host ('[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss'), `$Message)
}

function Test-BackendAvailable {
    try {
        Invoke-WebRequest -Uri 'http://localhost:5142/api/dashboard/summary' -UseBasicParsing -TimeoutSec 2 | Out-Null
        return `$true
    }
    catch {
        return `$false
    }
}

function Stop-ExistingBackend {
    `$connections = Get-NetTCPConnection -LocalPort 5142 -State Listen -ErrorAction SilentlyContinue
    foreach (`$connection in `$connections) {
        try {
            Stop-Process -Id `$connection.OwningProcess -Force -ErrorAction SilentlyContinue
            Write-Step "Alter Backend-Prozess beendet: PID `$(`$connection.OwningProcess)"
        }
        catch {
        }
    }
}

if (-not (Test-Path `$postgresData)) {
    New-Item -ItemType Directory -Force -Path `$postgresData | Out-Null
}

Set-Location `$repoRoot
`$env:POSTGRES_HOST_DATA_DIR = (`$postgresData -replace '\\', '/')

Write-Step 'PostgreSQL per Docker wird gestartet...'
docker compose up -d | Out-Null

`$connectionString = 'Host=localhost;Port=5432;Database=invoicewizard;Username=invoicewizard;Password=invoicewizard'
`$env:ConnectionStrings__PostgreSql = `$connectionString
`$env:INVOICEWIZARD_API_BASEURL = 'http://localhost:5142/'

Stop-ExistingBackend

Write-Step 'Backend wird gestartet...'
`$backendArgs = @(
    '-NoExit',
    '-Command',
    "`$env:ConnectionStrings__PostgreSql = '`$connectionString'; Set-Location '`$runtimeRoot'; dotnet '`$backendDll' --urls http://localhost:5142 2>&1 | Tee-Object -FilePath '`$backendLog'"
)
Start-Process -FilePath `$powerShellExe -ArgumentList `$backendArgs | Out-Null

for (`$i = 0; `$i -lt 30; `$i++) {
    Start-Sleep -Seconds 1
    if (Test-BackendAvailable) {
        Write-Step 'Backend ist erreichbar. WPF-App wird gestartet.'
        Start-Process `$wpfExe | Out-Null
        exit 0
    }
}

Write-Step 'Backend hat nicht rechtzeitig geantwortet.'
Write-Step "Bitte die Datei '`$backendLog' pruefen."
Read-Host 'Bitte Enter druecken'
"@

Set-Content -Path $launcherPath -Value $launcherContent

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($desktopShortcut)
$shortcut.TargetPath = $powerShellExe
$shortcut.Arguments = "-ExecutionPolicy Bypass -File `"$launcherPath`""
$shortcut.WorkingDirectory = $RuntimeRoot
$shortcut.IconLocation = 'C:\Windows\System32\shell32.dll,220'
$shortcut.Description = 'Startet InvoiceWizard von Laufwerk E'
$shortcut.Save()

Write-Host "Runtime nach $RuntimeRoot veroeffentlicht."
Write-Host "PostgreSQL-Datenpfad: $postgresDir"
Write-Host "Launcher: $launcherPath"
Write-Host "Desktop-Verknuepfung aktualisiert: $desktopShortcut"
