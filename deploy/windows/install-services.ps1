$ErrorActionPreference = 'Stop'

param(
    [string]$BackendServiceName = 'InvoiceWizardBackend',
    [string]$WebServiceName = 'InvoiceWizardWeb',
    [string]$BackendUrl = 'http://0.0.0.0:8080',
    [string]$WebUrl = 'http://0.0.0.0:8081',
    [string]$ConnectionString = 'Host=localhost;Port=5432;Database=invoicewizard;Username=invoicewizard;Password=change-me',
    [string]$BackendBaseUrlForWeb = 'http://127.0.0.1:8080/'
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$deployDir = Split-Path -Parent $scriptDir
$rootDir = Split-Path -Parent $deployDir
$backendProject = Join-Path $rootDir 'InvoiceWizard.Backend\InvoiceWizard.Backend.csproj'
$webProject = Join-Path $rootDir 'InvoiceWizard.Web\InvoiceWizard.Web.csproj'

$backendDll = Join-Path $rootDir 'InvoiceWizard.Backend\bin\Release\net9.0\InvoiceWizard.Backend.dll'
$webDll = Join-Path $rootDir 'InvoiceWizard.Web\bin\Release\net9.0\InvoiceWizard.Web.dll'

Set-Location $rootDir

dotnet publish $backendProject -c Release | Out-Null
dotnet publish $webProject -c Release | Out-Null

if (-not (Test-Path $backendDll)) {
    throw "Backend DLL nicht gefunden: $backendDll"
}

if (-not (Test-Path $webDll)) {
    throw "Web DLL nicht gefunden: $webDll"
}

$backendBinary = 'dotnet'
$backendArgs = '"' + $backendDll + '" --urls ' + $BackendUrl
$webBinary = 'dotnet'
$webArgs = '"' + $webDll + '" --urls ' + $WebUrl

$backendExists = Get-Service -Name $BackendServiceName -ErrorAction SilentlyContinue
if ($backendExists) {
    sc.exe stop $BackendServiceName | Out-Null
    sc.exe delete $BackendServiceName | Out-Null
    Start-Sleep -Seconds 2
}

$webExists = Get-Service -Name $WebServiceName -ErrorAction SilentlyContinue
if ($webExists) {
    sc.exe stop $WebServiceName | Out-Null
    sc.exe delete $WebServiceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $BackendServiceName binPath= "`"$backendBinary`" $backendArgs" start= auto | Out-Null
sc.exe create $WebServiceName binPath= "`"$webBinary`" $webArgs" start= auto | Out-Null

sc.exe description $BackendServiceName "InvoiceWizard ASP.NET Core Backend" | Out-Null
sc.exe description $WebServiceName "InvoiceWizard Blazor Web Frontend" | Out-Null

reg.exe add "HKLM\SYSTEM\CurrentControlSet\Services\$BackendServiceName" /v Environment /t REG_MULTI_SZ /d "ConnectionStrings__PostgreSql=$ConnectionString" /f | Out-Null
reg.exe add "HKLM\SYSTEM\CurrentControlSet\Services\$WebServiceName" /v Environment /t REG_MULTI_SZ /d "Backend__BaseUrl=$BackendBaseUrlForWeb" /f | Out-Null

sc.exe start $BackendServiceName | Out-Null
sc.exe start $WebServiceName | Out-Null

Write-Host 'Windows-Dienste wurden installiert bzw. aktualisiert.'
Write-Host "Backend-Service: $BackendServiceName ($BackendUrl)"
Write-Host "Web-Service:     $WebServiceName ($WebUrl)"
Write-Host 'Firewall und Reverse Proxy bei Bedarf noch separat konfigurieren.'
