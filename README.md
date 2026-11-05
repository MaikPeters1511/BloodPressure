# CleanArchitecture App


![clean.png](Images%2Fclean.png)

### Web API with .NET 10 and Blazor WebAssembly

DI (Dependency Injection)
EntityFrameworkCore 10

## Oberfläche (Blazor WebAssembly)

Nach der Anmeldung stehen fünf Bereiche bereit: **Messwerte** (Liste mit Kategorie, Anlegen, Löschen), **Auswertung** (Kennzahlen und Verlaufsdiagramm je Tag/Woche/Monat), **Export** (CSV oder PDF für einen Zeitraum), **Erinnerungen** (bis zu 5 tägliche Uhrzeiten) und **Hinweise** (Warnungen mit Zähler in der Navigation). Texte sind DE/EN; die API-Adresse steht in `CleanArchitecture.Presentation.UI/wwwroot/appsettings.json` (`Api:BaseUrl`), die Browser-Herkunft der UI muss in der API unter `Cors:AllowedOrigins` freigegeben sein.

## MAUI-App (iOS, Android, Mac Catalyst)

Stand: Anmeldung (Registrieren, Abmelden, gespeicherte Anmeldung im sicheren Speicher), Messwerte (Liste mit Nachladen, Anlegen, Löschen), Auswertung (Kennzahlen und Verlaufsdiagramm je Tag/Woche/Monat) und Export (CSV/PDF über den Teilen-Dialog). Erinnerungen und Hinweise folgen (siehe `UserStories/maui-app-api-anbindung.openspec.md`).

- Logik (ViewModels, Sitzung, Texte DE/EN) liegt in `CleanArchitecture.Presentation.Maui.Core` und ist über `dotnet test --project Tests/CleanArchitecture.Maui.Tests` testbar; Entscheidung in `docs/adr/0004-maui-core-und-mvvm-toolkit.md`.
- **API-Adresse:** `CleanArchitecture.Presentation.Maui/appsettings.json` (`Api:BaseUrl`, vor einem Release-Build setzen, nur HTTPS); Debug-Builds nutzen `appsettings.Debug.json` (`http://localhost:5000`, im Android-Emulator automatisch `10.0.2.2`).
- Bauen z. B. für den iOS-Simulator: `dotnet build CleanArchitecture.Presentation.Maui -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` (bei abweichender Xcode-Version zusätzlich `-p:ValidateXcodeVersion=false`).
- Bekannt: Der Mac-Catalyst-Build startet unter macOS 27 nicht (Scene-Lifecycle-Abbruch schon in der Projektvorlage); Android- und Windows-Build sind ungeprüft.

## Container-Image

Nach jedem erfolgreichen CI-Lauf auf `main` veröffentlicht der Workflow `CD` das Image der API in der GitHub Container Registry:

```bash
docker pull ghcr.io/<owner>/bloodpressure-api:latest   # <owner> in Kleinbuchstaben
```

Tags: `latest` und `sha-<Commit>`. Das Image läuft als Nicht-Root-Benutzer und lauscht auf Port 8080.

Konfiguration nur über Umgebungsvariablen:

| Variable | Bedeutung |
|---|---|
| `ConnectionStrings__bloodpressuredb` | PostgreSQL-Connection-String (erforderlich) |
| `Cors__AllowedOrigins__0` | Erlaubte Browser-Herkunft für das Frontend (weitere mit `__1`, ...) |
| `ASPNETCORE_ENVIRONMENT` | `Production` (Standard). Die Migrationen werden nur in `Development` automatisch angewendet. |

Liveness: `GET /alive` (nur "self"-Check, in allen Umgebungen). Der vollständige Health-Check `/health` ist nur in `Development` erreichbar.
