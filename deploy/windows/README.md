# Windows-Betrieb

InvoiceWizard kann auch direkt auf einem Windows-Rechner laufen. Dafuer gibt es drei sinnvolle Betriebsarten.

## 1. Lokal mit `dotnet run`

Geeignet fuer Entwicklung, Tests und Einzelplatzbetrieb.

Voraussetzungen:

- .NET SDK 9
- PostgreSQL erreichbar

Start:

```powershell
./deploy/windows/start-local.ps1
```

Dabei werden Backend und Web-App in zwei PowerShell-Fenstern gestartet.

Die WPF-App kannst du zusaetzlich separat starten:

```powershell
./deploy/windows/start-wpf.ps1
```

Wenn Laufwerk `C:` knapp ist, kannst du Backend-Laufzeit und PostgreSQL-Daten auf `E:` auslagern:

```powershell
./deploy/windows/move-to-e-drive.ps1
```

Danach liegt die Runtime unter `E:\InvoiceWizard` und die Desktop-Verknuepfung startet von dort.

Standard-URLs:

- Backend: `http://localhost:5142`
- Web: `http://localhost:5286`

Die WPF-App nutzt standardmaessig bereits `http://localhost:5142/`.

## 2. Docker Desktop

Geeignet fuer den einfachsten stabilen Windows-Betrieb.

Voraussetzungen:

- Docker Desktop

Start:

```powershell
./deploy/windows/start-docker-desktop.ps1
```

Standard-URLs:

- Backend: `http://localhost:8080`
- Web: `http://localhost:8081`

Wenn die WPF-App gegen den Docker-Server laufen soll:

```powershell
$env:INVOICEWIZARD_API_BASEURL='http://localhost:8080/'
```

## 3. Als Windows-Dienst

Geeignet fuer Dauerbetrieb auf einem festen Windows-PC oder kleinen Server.

Voraussetzungen:

- PowerShell als Administrator
- PostgreSQL lokal oder im Netzwerk erreichbar

Beispiel:

```powershell
./deploy/windows/install-services.ps1 `
  -ConnectionString 'Host=localhost;Port=5432;Database=invoicewizard;Username=invoicewizard;Password=dein-passwort' `
  -BackendBaseUrlForWeb 'http://127.0.0.1:8080/'
```

Danach laufen zwei Dienste:

- `InvoiceWizardBackend`
- `InvoiceWizardWeb`

Standard-Ports:

- Backend: `8080`
- Web: `8081`

## Zugriff vom Handy oder anderen Geraeten

Wenn andere Geraete im Heimnetz zugreifen sollen:

- statt `localhost` die IP deines Windows-Rechners verwenden
- Ports in der Windows-Firewall freigeben
- optional einen Reverse Proxy fuer HTTPS davorschalten

Beispiele fuer Reverse Proxy und HTTPS liegen bereits im Deploy-Ordner:

- `deploy/Caddyfile.example`
- `deploy/nginx.invoicewizard.conf.example`
