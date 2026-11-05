# CleanArchitecture App


![clean.png](Images%2Fclean.png)

### Web API with .NET 10 and Blazor WebAssembly

DI (Dependency Injection)
EntityFrameworkCore 10

## Oberfläche (Blazor WebAssembly)

Nach der Anmeldung stehen fünf Bereiche bereit: **Messwerte** (Liste mit Kategorie, Anlegen, Löschen), **Auswertung** (Kennzahlen und Verlaufsdiagramm je Tag/Woche/Monat), **Export** (CSV oder PDF für einen Zeitraum), **Erinnerungen** (bis zu 5 tägliche Uhrzeiten) und **Hinweise** (Warnungen mit Zähler in der Navigation). Texte sind DE/EN; die API-Adresse steht in `CleanArchitecture.Presentation.UI/wwwroot/appsettings.json` (`Api:BaseUrl`), die Browser-Herkunft der UI muss in der API unter `Cors:AllowedOrigins` freigegeben sein.

## Profil & Einstellungen

Web (`/profile`) und MAUI-App (Schaltfläche „Profil“ auf den Messwerten) haben eine Seite mit drei Teilen:

- **Darstellung** (System / Hell / Dunkel): gilt je Gerät und wird lokal gespeichert (Browser: `localStorage`, App: `Preferences`), nicht am Server. Im Web setzt `data-theme` am `<html>`-Element das Schema; ohne Wahl gilt das Systemschema.
- **Sprache** (System / Deutsch / English): gilt ebenfalls je Gerät und wird lokal gespeichert (Browser: `localStorage` `bp.lang`, App: `Preferences` `bp.language`), nicht am Server. „System“ folgt der Browser- bzw. Gerätesprache (Englisch, sonst Deutsch). Der Wechsel wirkt sofort auf Texte, Zahlen- und Datumsformate; im Web bleiben ungespeicherte Eingaben erhalten, die App baut die Oberfläche neu auf (Anmeldung bleibt). Technisch: Das Web nutzt einen eigenen Lokalisierer (`LanguageLocalizer`), weil Blazor WebAssembly die Kultur des Threads beim Start festlegt, und lädt dafür beide Sprachpakete (`loadAllSatelliteResources` in `wwwroot/index.html`).
- **Persönliche Angaben** (Alter, Größe in cm, Gewicht in kg, alle optional): am Server je Nutzer unter `GET/PUT/DELETE /api/v1/profile`. Gespeichert wird das Geburtsjahr, angezeigt das Alter. Dazu eine BMI-Zahl **ohne** Bewertung und mit dem Hinweis „keine Diagnose“. Die Angaben stehen nicht in Exporten.

## MAUI-App (iOS, Android, Mac Catalyst)

Stand: Anmeldung (Registrieren, Abmelden, gespeicherte Anmeldung im sicheren Speicher), Messwerte (Liste mit Nachladen, Anlegen, Löschen), Auswertung (Kennzahlen und Verlaufsdiagramm je Tag/Woche/Monat) Export (CSV/PDF über den Teilen-Dialog), Erinnerungen (Zeitzone, bis zu 5 Uhrzeiten) und Hinweise (Warnungen, als gelesen markieren, Zähler im Tab-Titel). Barrierefreiheit: statischer XAML-Test in der CI, Geräte-Anleitung in `docs/a11y/maui-screenreader-pruefung.md` (Screenreader-Durchlauf noch offen) (siehe `UserStories/maui-app-api-anbindung.openspec.md`).

- Logik (ViewModels, Sitzung, Texte DE/EN) liegt in `CleanArchitecture.Presentation.Maui.Core` und ist über `dotnet test --project Tests/CleanArchitecture.Maui.Tests` testbar; Entscheidung in `docs/adr/0004-maui-core-und-mvvm-toolkit.md`.
- **API-Adresse:** `CleanArchitecture.Presentation.Maui/appsettings.json` (`Api:BaseUrl`, vor einem Release-Build setzen, nur HTTPS); Debug-Builds nutzen `appsettings.Debug.json` (`http://localhost:5000`, im Android-Emulator automatisch `10.0.2.2`).
- Bauen z. B. für den iOS-Simulator: `dotnet build CleanArchitecture.Presentation.Maui -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` (bei abweichender Xcode-Version zusätzlich `-p:ValidateXcodeVersion=false`).
- iOS/macOS 26+ verlangen die Scene-Lifecycle-Anbindung (`SceneDelegate` plus `UIApplicationSceneManifest` in der Info.plist unter `Platforms/iOS` und `Platforms/MacCatalyst`); ohne sie bricht die App beim Start ab. Geprüft: iOS-26.5-Simulator, iOS-27-Simulator, Mac Catalyst unter macOS 27. Nach Änderungen an der Info.plist `bin`/`obj` des Zielframeworks löschen, sonst bleibt die alte Plist im App-Paket.
- **Android** (geprüft am 2026-11-05: Emulator API 35, Build mit .NET 10 / Android-Workload 36.1): `dotnet build CleanArchitecture.Presentation.Maui -f net10.0-android -p:AndroidSdkDirectory=$HOME/Library/Android/sdk -p:EmbedAssembliesIntoApk=true`. Der Schalter `EmbedAssembliesIntoApk=true` ist für einen per `adb install` installierten Debug-APK nötig; ohne ihn bricht die App beim Start mit „No assemblies found … Fast Deployment“ ab. Die API muss auf `127.0.0.1:5000` lauschen (der Emulator erreicht sie als `10.0.2.2:5000`).
- Windows-Build ist ungeprüft (nur unter Windows baubar).
- Hinweistexte: Der Server liefert sie auf Deutsch; Web-UI und App bilden die Texte bekannter Typen (kritischer Wert, Erinnerung) selbst in der Oberflächensprache, nur unbekannte Typen zeigen den Servertext.

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
