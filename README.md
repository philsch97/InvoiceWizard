# InvoiceWizard

Die Loesung arbeitet jetzt mit einer produktionsnahen Zielarchitektur:

- `InvoiceWizard.Backend` ist die zentrale ASP.NET Core Web API mit PostgreSQL und EF-Migrations
- `InvoiceWizard.Web` ist die responsive mobile Web-App
- `InvoiceWizard` (WPF) nutzt dieselbe API fuer Kunden, Projekte, Arbeitszeiten, Import, Materialzuweisungen und Analytics

## Lokal entwickeln

1. PostgreSQL starten, zum Beispiel mit `docker compose up -d`
2. Backend starten
3. Web-App oder WPF-App starten

Beispiel fuer lokales Restore und Build mit isoliertem NuGet/Home:

```powershell
$env:DOTNET_CLI_HOME='C:\Users\phili\source\repos\InvoiceWizard\.dotnet-home'
$env:NUGET_PACKAGES='C:\Users\phili\source\repos\InvoiceWizard\.dotnet-home\.nuget\packages'
dotnet restore InvoiceWizard.sln --configfile .\NuGet.Config
dotnet build InvoiceWizard.sln --configfile .\NuGet.Config
```

## Datenbank

- Backend nutzt jetzt EF Core Migrations statt `EnsureCreated`
- die Initial-Migration liegt im Backend-Projekt und wird beim Start automatisch angewendet
- fuer lokale Entwicklung kann `InvoiceWizard.Backend/appsettings.Development.json` genutzt werden
- fuer Server/NAS sollte `ConnectionStrings__PostgreSql` per Umgebung gesetzt werden

## Wichtige Konfiguration

- WPF nutzt standardmaessig `http://localhost:5142/`
- alternativ kann die URL ueber `INVOICEWIZARD_API_BASEURL` gesetzt werden
- die Web-App spricht lokal standardmaessig `https://localhost:7181/` an
- im Container-Setup nutzt die Web-App intern `http://backend:8080/`

## Produktion / NAS

1. `deploy/.env.example` nach `deploy/.env` kopieren
2. Passwort, Ports und ggf. Hostnamen anpassen
3. Deployment starten

Linux / NAS:

```bash
chmod +x deploy/release.sh
./deploy/release.sh
```

Windows / PowerShell:

```powershell
./deploy/release.ps1
```

Windows direkt ohne NAS/Container ist ebenfalls moeglich. Die drei Varianten sind in [deploy/windows/README.md](/C:/Users/phili/source/repos/InvoiceWizard/deploy/windows/README.md) beschrieben:

- lokal mit `dotnet run`
- mit Docker Desktop
- als Windows-Dienst

Manuell geht es ebenfalls so:

```bash
docker compose --env-file deploy/.env -f deploy/compose.server.yaml up -d --build
```

Enthalten sind:

- PostgreSQL 17
- Backend-Container auf Port `8080`
- Web-Container auf Port `8081`

## Reverse Proxy / HTTPS

Empfohlen fuer einfache Setups:

- Caddy mit [deploy/Caddyfile.example](/C:/Users/phili/source/repos/InvoiceWizard/deploy/Caddyfile.example)
  Vorteil: automatisches HTTPS mit sehr wenig Konfiguration

Alternative:

- nginx mit [deploy/nginx.invoicewizard.conf.example](/C:/Users/phili/source/repos/InvoiceWizard/deploy/nginx.invoicewizard.conf.example)

Empfohlener Aufbau:

- Domain `invoicewizard.example.com`
- Reverse Proxy nimmt HTTPS entgegen
- `/api/*` zeigt auf Backend `127.0.0.1:8080`
- `/` zeigt auf Web `127.0.0.1:8081`

## API

Wichtige Endpunkte:

- `GET /api/dashboard/summary`
- `GET/POST/PUT/DELETE /api/customers`
- `GET /api/projects`
- `POST /api/customers/{customerId}/projects`
- `DELETE /api/projects/{projectId}`
- `GET/POST/DELETE /api/worktimeentries`
- `PUT /api/worktimeentries/{id}/status`
- `POST /api/invoices`
- `GET/DELETE /api/invoicelines`
- `GET/POST/PUT/DELETE /api/allocations`
- `GET /api/analytics/details`
