# CleanArchitecture App


![clean.png](Images%2Fclean.png)

### Web API with .NET 10 and Blazor WebAssembly

DI (Dependency Injection)
EntityFrameworkCore 10

## Oberfläche (Blazor WebAssembly)

Nach der Anmeldung stehen fünf Bereiche bereit: **Messwerte** (Liste mit Kategorie, Anlegen, Löschen), **Auswertung** (Kennzahlen und Verlaufsdiagramm je Tag/Woche/Monat), **Export** (CSV oder PDF für einen Zeitraum), **Erinnerungen** (bis zu 5 tägliche Uhrzeiten) und **Hinweise** (Warnungen mit Zähler in der Navigation). Texte sind DE/EN; die API-Adresse steht in `CleanArchitecture.Presentation.UI/wwwroot/appsettings.json` (`Api:BaseUrl`), die Browser-Herkunft der UI muss in der API unter `Cors:AllowedOrigins` freigegeben sein.

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
