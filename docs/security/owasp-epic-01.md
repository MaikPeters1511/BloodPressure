# OWASP-Top-10-Prüfung Epic 1 (Authentifizierung und Benutzerbezug)

Stand: 2026-11-12 · Prüfer: sec-agent · Branch: `docs/epic01-owasp-review`
Grundlage: `UserStories/epics/epic-01-auth-benutzerbezug.openspec.md` (Abschnitt 6, Acceptance Criteria).
Maßstab: OWASP Top 10 (2021, A01–A10). Belege sind `Datei:Zeile` relativ zum Repository-Wurzelverzeichnis (Arbeitsstand der Prüfung).

## 1. Umfang und Methode

Geprüft (statische Codeprüfung, keine Codeänderung):

- API: `CleanArchitecture.Presentation.API` (`Program.cs`, Controller, `AuthorizedControllerBase`, `GlobalExceptionHandler`, `appsettings*.json`, `Dockerfile`, `launchSettings.json`)
- Eigentümerprüfung: Application-Handler und Infrastructure-Repositories (Messungen, Hinweise, Erinnerungen, Profil, Altdaten-Übernahme)
- ServiceDefaults (Health-Endpunkte), AppHost, CI/CD-Workflows, `.gitignore`/`.dockerignore`
- Frontends: Blazor-WASM (`Presentation.UI`: Token-Handling, Seitenschutz, Konfiguration) und MAUI (`Presentation.Maui`/`Maui.Core`: `SecureStorage`, Plattform-Manifeste, API-Endpunkt)
- Secrets-Suche über alle getrackten Dateien (`git grep` nach Passwort/Secret/Token/Key-Mustern, Schlüssel-Präfixen) sowie `git log -S` nach `LicenseKey`-Werten
- NuGet: `dotnet list <Projekt> package --vulnerable --include-transitive` für API, UI, Maui, Maui.Core, AppHost, Infrastructure, Contracts sowie für die gesamte Solution (alle 14 Projekte)
- Vorhandene Sicherheitstests (Titel/Inhalt gelesen): `Tests/CleanArchitecture.Tests/SecurityConventionTests.cs`, `AuthIntegrationTests.cs`, `NotificationApiTests.cs`, `ProfileTests.cs`, `ExportTests.cs`, `HealthEndpointTests.cs`, `VersioningAndRateLimitTests.cs`, `AcceptanceEvidenceTests.cs`

Nicht geprüft / Einschränkungen (ausdrücklich):

- Keine Laufzeitprüfung: API, Docker-Container, Aspire-AppHost und die Postgres-abhängigen Integrationstests (Testcontainers/`PostgresFixture`) wurden nicht gestartet; Testergebnisse wurden nicht neu erzeugt.
- Kein Penetrationstest, kein dynamischer Scan (ZAP o. Ä.); HTTP-Antwortheader wurden daher nicht live gemessen, sondern nur im Code gesucht.
- Verhalten von `MapIdentityApi` (Endpunktliste, Standard-Token-Lebensdauern, Lockout bei Login, Standard-Passwortregeln) stammt aus dem ASP.NET-Core-Framework und ist hier nicht durch Repository-Code belegt; die Angaben unten sind als Framework-Standard gekennzeichnet und sollten bei Bedarf per Laufzeittest bestätigt werden.
- Die MAUI-Plattformprojekte wurden nur über Manifeste/Quellcode bewertet; kein Gerätetest (Keychain/Keystore).
- Trivy-Image-Scan im CD-Workflow nicht ausgeführt.
- Git-Historie nur gezielt (`-S`) durchsucht, kein vollständiger Secret-Scanner (gitleaks/trufflehog) eingesetzt.

## 2. Ergebnis A01–A10

Legende: OK = kein Befund; Auffälligkeit = Maßnahme empfohlen (Risiko-ID siehe Abschnitt 6); n. a. = nicht anwendbar.

| ID | Thema | Befund | Beleg |
|----|-------|--------|-------|
| A01 | Broken Access Control | Auffälligkeit (gering): Kern OK, Lücken bei Nachweis und Frontend | siehe unten |
| A02 | Cryptographic Failures | Auffälligkeit (mittel): Token-Ablage Blazor, HSTS fehlt | siehe unten |
| A03 | Injection | OK | siehe unten |
| A04 | Insecure Design | Auffälligkeit (gering): Altdaten-Übernahme, offene Registrierung | siehe unten |
| A05 | Security Misconfiguration | Auffälligkeit (mittel): keine Security-Header, `AllowedHosts: *`, Rate-Limit hinter Proxy | siehe unten |
| A06 | Vulnerable Components | OK (Stand heute) | siehe unten |
| A07 | Identification and Authentication Failures | Auffälligkeit (mittel): Passwort-/Token-Policy nicht explizit, kein Test für Lockout, kein eigenes Limit für Login | siehe unten |
| A08 | Software and Data Integrity Failures | Auffälligkeit (gering): CI-Actions nicht auf SHA gepinnt, `latest`-Tag | siehe unten |
| A09 | Security Logging and Monitoring Failures | Auffälligkeit (gering): keine Sicherheitsereignisse protokolliert | siehe unten |
| A10 | SSRF | n. a. | siehe unten |

### A01 Broken Access Control

OK:
- Alle fünf Controller tragen `[Authorize]`, keiner `[AllowAnonymous]`: `CleanArchitecture.Presentation.API/Controllers/BloodPressuresController.cs:13`, `NotificationsController.cs:12`, `ProfileController.cs:13`, `RemindersController.cs:12`.
- Die Benutzerkennung stammt ausschließlich aus dem Token-Claim, nie aus dem Request: `CleanArchitecture.Presentation.API/Infrastructure/AuthorizedControllerBase.cs:9-10`; alle Controller geben `UserId` an die Messages weiter (z. B. `BloodPressuresController.cs:19,37,51,65,84`).
- Eigentümerfilter in jeder Abfrage: `CleanArchitecture.Infrastructure/Repository/BloodPressureRepository.cs:14` (Liste/Export), `:37` (Statistik), `:60` (Einzelzugriff, Basis für Update/Delete in `CleanArchitecture.Application/BloodPressures/BloodPressureHandler.cs:66-71,93-97`); `NotificationRepository.cs:12,18`; `ReminderRepository.cs:11,19`; `UserProfileRepository.cs:11,24`.
- Fremde Datensätze liefern 404 (Handler geben `null`/`false` zurück, Beispiel `NotificationHandler.cs:20-24`); durch Tests belegt: `Tests/CleanArchitecture.Tests/AuthIntegrationTests.cs:85,102-103`, `NotificationApiTests.cs:59`.
- Konventionstest erzwingt `[Authorize]`/`[AllowAnonymous]` je Controller: `Tests/CleanArchitecture.Tests/SecurityConventionTests.cs:13-24`.
- 401 ohne Token getestet: `AuthIntegrationTests.cs:46`, `ExportTests.cs:133`, `NotificationApiTests.cs:138`, `ProfileTests.cs:186`.
- CORS: außerhalb von Development nur konfigurierte Origins, leere Liste bedeutet keine Origin erlaubt (fail closed): `Program.cs:101-112`; Development ohne Konfiguration erlaubt alle Origins (`Program.cs:104-107`), nur dort.

Auffälligkeiten:
- Der Konventionstest erfasst nur MVC-Controller, nicht die per `MapIdentityApi` und `MapDefaultEndpoints` registrierten Minimal-Endpunkte; es gibt keinen Test, der alle Endpunkte des `EndpointDataSource` auf Autorisierung prüft (R-03). Das Acceptance Criterion "kein Endpunkt außer den freigegebenen ist anonym" ist damit für Identity-Endpunkte nur durch Framework-Wissen, nicht durch einen Test belegt.
- Blazor schützt Seiten nur clientseitig (`Session.IsAuthenticated`, z. B. `Presentation.UI/Pages/Measurements.razor:145`, `Profile.razor:124`); es gibt weder `[Authorize]` noch `AuthorizeView` noch `AuthenticationStateProvider` im UI-Projekt (Suche ohne Treffer). Da die Daten ausschließlich über die abgesicherte API kommen, ist das kein Datenleck, aber die Rollenvorgabe der Frontend-Absicherung ist nicht umgesetzt (R-08).
- Legacy-Weiterleitung `/api/BloodPressure`, `/api/reminders`, `/api/notifications` antwortet anonym mit 308 (nur `Location`-Header, keine Daten): `Program.cs:123-138`.

### A02 Cryptographic Failures

OK:
- Keine eigene Kryptografie; Passwort-Hashing und Token-Schutz durch ASP.NET Core Identity (`Program.cs:30-37`).
- Produktion erzwingt HTTPS-Weiterleitung: `Program.cs:149-152`.
- MAUI legt Tokens in `SecureStorage` (Keychain/Keystore): `CleanArchitecture.Presentation.Maui/Services/MauiSecureStore.cs:5-16`, genutzt in `Presentation.Maui.Core/Services/SessionService.cs:32-34,109-111`.
- MAUI verweigert Klartext-HTTP in Release-Builds: `Presentation.Maui.Core/Services/ApiEndpoint.cs:18-21`; Android-Cleartext nur unter `#if DEBUG`: `Presentation.Maui/Platforms/Android/MainApplication.cs:6-11`. iOS/MacCatalyst erlauben nur lokales Netz (`NSAllowsLocalNetworking`, `Platforms/iOS/Info.plist:22-25`).
- Datenbank-Verbindung kommt aus Konfiguration/Aspire (`CleanArchitecture.Infrastructure/DependencyInjection.cs:24`).

Auffälligkeiten:
- Blazor speichert Access- und Refresh-Token in `sessionStorage`, damit für JavaScript der Seite lesbar (XSS-Folgeschaden): `Presentation.UI/Services/SessionTokenStorage.cs:5-15`, Schlüssel `Presentation.UI/Services/AuthSession.cs:13-15`. Das Risiko ist im Code selbst dokumentiert (`SessionTokenStorage.cs:5-8`). Mildernd: Tab-bezogen, kein `MarkupString` mit ungeprüften Daten (einzige Stelle `Presentation.UI/Components/LineChart.razor:46-47` kodiert mit `HtmlEncode`) (R-04).
- Kein HSTS (`UseHsts`/`Strict-Transport-Security` im Code nicht vorhanden); nur `UseHttpsRedirection` (`Program.cs:151`) (R-05).
- Android: `android:allowBackup="true"` (`Presentation.Maui/Platforms/Android/AndroidManifest.xml:3`). Tokens liegen zwar im Keystore, aber Backup/Restore des Keystore-gebundenen Speichers ist plattformseitig fehleranfällig und vergrößert die Angriffsfläche (R-09).

### A03 Injection

OK:
- Datenzugriff nur über EF Core/LINQ mit Parametern, keine Roh-SQL-Aufrufe im Repository (`BloodPressureRepository.cs`, `NotificationRepository.cs`, `ReminderRepository.cs`, `UserProfileRepository.cs`, `LegacyDataClaimer.cs:20-21` nutzt `ExecuteUpdateAsync`).
- Eingaben werden validiert (`ValidateRequestFilter` an allen Controllern: `Program.cs:46`; Validatoren in `CleanArchitecture.Application/*/…Validator.cs`).
- XSS: kein ungeprüftes `MarkupString`/`innerHTML` im Blazor-UI (`LineChart.razor:46-47` kodiert Text); Razor kodiert standardmäßig.
- Nicht geprüft: CSV-Export auf Formel-Injektion (Inhalte laut Code numerisch/Datum; Writer nicht im Detail bewertet).

### A04 Insecure Design

OK:
- Mandantentrennung im Design verankert (UserId in Messages, "kein HttpContext in Handlern": `CleanArchitecture.Application/BloodPressures/Messages.cs:5`).
- Optimistische Nebenläufigkeit (If-Match/412) gegen Lost Updates: `BloodPressureHandler.cs:79-84`.
- Entscheidung dokumentiert in `docs/adr/0001-identity-api.md`.

Auffälligkeiten:
- Altdaten gehören dem ersten registrierten Nutzer (`CleanArchitecture.Infrastructure/LegacyDataClaimer.cs:13-22`, Aufruf im Register-Filter `Program.cs:161-179`). Da die Registrierung offen ist, übernimmt bei einer öffentlich erreichbaren Erstinstallation mit Altdaten die erste beliebige Registrierung fremde Gesundheitsdaten. Die Zählung `Count() != 1` und das Update sind nicht atomar (theoretisches Race bei gleichzeitigen Registrierungen) (R-06).
- Registrierung ist ohne E-Mail-Bestätigung offen (`RequireConfirmedEmail` nicht gesetzt, `Program.cs:30-37`); Konto-Enumeration über Registrierungsfehler ist bei Identity-API-Standard möglich (R-07).

### A05 Security Misconfiguration

OK:
- Zentrales Exception Handling ohne Stacktrace/Interna im Response: `Program.cs:99,121`, `CleanArchitecture.Presentation.API/Infrastructure/GlobalExceptionHandler.cs:54-62`; Test `Tests/CleanArchitecture.Tests/AcceptanceEvidenceTests.cs:70`.
- OpenAPI/Swagger UI nur in Development: `Program.cs:140-143`; `/health` (mit DB-Check) nur in Development, `/alive` ohne Details überall: `CleanArchitecture.ServiceDefaults/Extensions.cs:115-125`; Test `Tests/CleanArchitecture.Tests/HealthEndpointTests.cs:9,19`.
- Automatische DB-Migration nur in Development: `Program.cs:145-147`.
- Container läuft als Nicht-Root-Benutzer: `CleanArchitecture.Presentation.API/Dockerfile:2`.
- Rate Limiting aktiv (100/min je Nutzer bzw. IP, 429 mit Retry-After): `Program.cs:65-98`; Test `VersioningAndRateLimitTests.cs:64`.
- Keine `EnableSensitiveDataLogging`/`EnableDetailedErrors`/`UseDeveloperExceptionPage` im Code (Suche ohne Treffer).

Auffälligkeiten:
- Es werden keine Security-Header gesetzt (`X-Content-Type-Options`, `Content-Security-Policy`, `Referrer-Policy`, `X-Frame-Options`/`frame-ancestors`); Suche im gesamten Code ohne Treffer (R-05). Für das Blazor-Hosting (`wwwroot/index.html` enthält Inline-Skripte, `index.html:12,57,75`) ist zudem keine CSP vorhanden; Hosting-Ort des UI ist im Repository nicht definiert (nicht geprüft).
- `"AllowedHosts": "*"` in `CleanArchitecture.Presentation.API/appsettings.json:11` (R-10).
- Rate-Limit-Partition anonym nach `RemoteIpAddress` (`Program.cs:77-79`); `ForwardedHeaders` ist nicht konfiguriert. Hinter einem Reverse Proxy/Load Balancer teilen sich alle anonymen Clients eine Partition (ein Angreifer sperrt Login/Registrierung für alle) bzw. die IP-Begrenzung greift nicht pro Client (R-02).
- `AppHost` startet pgAdmin (`CleanArchitecture.AppHost/AppHost.cs:5`) und `WithExternalHttpEndpoints()` (`:13`); gilt nur für lokale Orchestrierung, aber Zugangsdaten sind Aspire-generiert (siehe Secrets). Laufzeit nicht geprüft.

### A06 Vulnerable and Outdated Components

OK (Stand 2026-11-12, NuGet-Feed `api.nuget.org`):
- `dotnet list <Projekt> package --vulnerable --include-transitive` meldet für alle geprüften Projekte "keine anfälligen Pakete" (API, UI, Maui, Maui.Core, AppHost, Infrastructure, Contracts, Domain, Application, ServiceDefaults, drei Testprojekte; zusätzlich Solution-weit).
- Aspire: `Aspire.Hosting 13.6.5` (aufgelöst, AppHost), `Aspire.Hosting.PostgreSQL 13.6.5` (`CleanArchitecture.AppHost/CleanArchitecture.AppHost.csproj:13`).
- MessagePack: im AppHost transitiv aufgelöst `MessagePack 2.5.307` (nicht 2.5.302; Auflösung aus `CleanArchitecture.AppHost/obj/project.assets.json`, `dotnet list package --include-transitive`). Für diese Version meldet der NuGet-Audit kein Advisory. Das zuvor bekannte moderate Advisory zu 2.5.302 ist damit durch die Aspire-Aktualisierung nicht mehr offen. Betroffen wäre ohnehin nur das lokale AppHost-Werkzeug, nicht die API-Laufzeit.
- CI bricht bei High/Critical ab: `.github/workflows/ci.yml:63-71`; Image-Scan (Trivy) vor dem Push: `.github/workflows/cd.yml:63-70`.

Hinweis: Das Prüfergebnis ist ein Momentbild; das Advisory-Set ändert sich. Die CI wertet Moderate-Funde nur als Ausgabe (`ci.yml:63-64`).

### A07 Identification and Authentication Failures

OK:
- Lockout explizit aktiviert: 5 Fehlversuche, 5 Minuten, auch für neue Nutzer: `Program.cs:33-35`.
- Bearer-Token/Refresh über Identity-API; Login liefert Access- und Refresh-Token (Test `AuthIntegrationTests.cs:58-70`).
- Blazor und MAUI erneuern Token automatisch und melden bei fehlgeschlagener Erneuerung ab: `Presentation.UI/Services/AuthSession.cs:89-98`, `Presentation.Maui.Core/Services/SessionService.cs:87-96`.
- Keine Anmeldedaten in Logs (Suche nach `Log*`-Aufrufen: nur Ausnahmen/Zeitzonen/Pfade; `GlobalExceptionHandler.cs:56` loggt Methode und Pfad).

Auffälligkeiten:
- Passwortregeln und Token-Lebensdauer sind nicht explizit konfiguriert (`Program.cs:30-37`; kein `Password`-Block, kein `BearerTokenOptions`). Es gelten die Framework-Standards (Mindestlänge 6, Ziffer/Groß/Klein/Sonderzeichen; Access-Token 1 Stunde, Refresh-Token 14 Tage; nicht durch Laufzeittest bestätigt). Für Gesundheitsdaten ist eine bewusste Festlegung (Mindestlänge 12, kürzere Refresh-Dauer) empfohlen; das Epic nennt "Token-Lebensdauer, Passwort-Policy" ausdrücklich als Risiko (`epic-01...:68`) (R-01).
- Für das Lockout gibt es keinen Test (Suche nach `Lockout` in `Tests/CleanArchitecture.Tests`: ohne Treffer). T1.4 "Lockout" ist damit nur im Code, nicht durch ein Scenario belegt (R-03).
- Login/Registrierung/Refresh unterliegen nur dem globalen Limit von 100/min je IP (`Program.cs:66,80-85`), kein strengeres Limit für `/api/auth/*` (R-02).
- Keine E-Mail-Bestätigung, kein MFA (Identity-API-Standard, `Program.cs:30-37`) (R-07).
- Refresh-Token wird bei Logout nur lokal gelöscht (`AuthSession.cs:55,117-126`, `SessionService.cs:53,115-124`); es gibt keinen serverseitigen Widerruf (Identity-API bietet keinen Standard-Revoke-Endpunkt; nicht geprüft) (R-01).

### A08 Software and Data Integrity Failures

OK:
- Migrationen sind versioniert, Altdaten-Strategie getestet (`Tests/CleanArchitecture.Tests/MigrationDataTests.cs`, Titel gelesen).
- Trivy-Scan vor dem Veröffentlichen, minimale Workflow-Berechtigungen (`contents: read`, `packages: write` nur im Publish-Job): `.github/workflows/cd.yml:10-11,24-26`.
- CD läuft nur nach erfolgreicher CI auf `main` für `push`-Events: `cd.yml:3-8,20`.

Auffälligkeiten:
- GitHub Actions sind nur auf Major-Tags gepinnt (`actions/checkout@v4`, `docker/build-push-action@v7`, `aquasecurity/trivy-action@v0.36.0` u. a., `ci.yml:29,31`, `cd.yml:28-65`), nicht auf Commit-SHA (R-11).
- Veröffentlichtes Image bekommt zusätzlich den beweglichen Tag `latest` (`cd.yml:48`); keine Image-Signatur/SBOM im Workflow (R-11).

### A09 Security Logging and Monitoring Failures

OK:
- Unbehandelte Ausnahmen werden serverseitig protokolliert, nicht an Clients ausgegeben: `GlobalExceptionHandler.cs:56-61`.
- Keine sensiblen Daten (Passwörter, Token, Messwerte) in den vorhandenen Log-Aufrufen; Testbeleg, dass Fehlertexte keine Interna enthalten: `AcceptanceEvidenceTests.cs:70`, MAUI-Tests (`Tests/CleanArchitecture.Maui.Tests/LoginViewModelTests.cs:99`).
- OpenTelemetry für Logs/Metriken/Traces vorhanden: `CleanArchitecture.ServiceDefaults/Extensions.cs:49-76`.

Auffälligkeiten:
- Sicherheitsereignisse (fehlgeschlagene Logins, Lockouts, 401/403, 429-Ablehnungen) werden nicht gezielt protokolliert oder alarmiert (`Program.cs:87-97` setzt nur die Antwort; Identity-Standardlogs hängen vom Log-Level ab, `appsettings.json:3-6`, nicht geprüft) (R-12).
- Die `ExceptionHandler`-Details für 400/409/412 geben `exception.Message` zurück (`GlobalExceptionHandler.cs:24,33,42,51`); die Texte sind laut Code statische Fachmeldungen (z. B. `PreconditionFailedException.cs:4`, `UserProfile.cs:33`). Kein Befund, aber bei künftigen Exceptions darauf achten.

### A10 Server-Side Request Forgery

n. a.: Die API ruft keine benutzergesteuerten URLs ab; in Epic 1 gibt es keinen ausgehenden HTTP-Aufruf aus Benutzereingaben (`ConfigureHttpClientDefaults` nur für Service-Discovery/Resilience, `ServiceDefaults/Extensions.cs:29-36`). Die KI-/Importfunktionen (Epic 9/10) sind nicht Gegenstand dieser Prüfung.

## 3. Freigegebene anonyme Endpunkte (belegt)

Alle MVC-Controller sind `[Authorize]` (Belege in A01). Anonym erreichbar sind ausschließlich:

| Endpunkt | Zweck | Beleg |
|----------|-------|-------|
| `GET /alive` | Liveness, nur Selbstprüfung ohne Abhängigkeiten/Details, in allen Umgebungen | `CleanArchitecture.ServiceDefaults/Extensions.cs:115-118`; kein `RequireAuthorization`; Test `Tests/CleanArchitecture.Tests/HealthEndpointTests.cs:9` |
| `GET /health` | Vollständiger Health-Check (inkl. DB), **nur Development** | `ServiceDefaults/Extensions.cs:120-124`; Test `HealthEndpointTests.cs:19` (nicht exponiert außerhalb Development) |
| `GET /openapi/v1.json`, Swagger UI | API-Dokumentation, **nur Development** | `Program.cs:140-143` |
| `POST /api/auth/register`, `/api/auth/login`, `/api/auth/refresh` | Registrierung, Anmeldung, Token-Erneuerung | `Program.cs:161` (`MapIdentityApi`, Framework-Standard: ohne Autorisierung); Login/Register/Refresh-Verwendung in `Contracts/Auth.cs:19-25` und `Tests/CleanArchitecture.Tests/AuthIntegrationTests.cs:37-38,62-64` |
| `/api/auth/confirmEmail`, `resendConfirmationEmail`, `forgotPassword`, `resetPassword` | weitere von `MapIdentityApi` registrierte anonyme Endpunkte | `Program.cs:161`; **Framework-Standard, nicht im Repository belegt und nicht per Test/Laufzeit geprüft.** Ohne registrierten `IEmailSender` ist kein E-Mail-Versand konfiguriert (kein `IEmailSender` im Code gefunden). Der Epic-Text nennt nur Login und Registrierung als freigegeben, sie sind daher nicht ausdrücklich freigegeben (R-03). |
| `/api/BloodPressure*`, `/api/reminders*`, `/api/notifications*` (unversioniert) | 308-Weiterleitung auf `/api/v1/...`, ohne Daten | `Program.cs:123-138`; läuft vor der Authentifizierung, liefert nur `Location` |

Authentifizierungspflichtig (Framework-Standard): `/api/auth/manage/*` (Info, 2FA).

Aussage zum Acceptance Criterion "kein Endpunkt außer den ausdrücklich freigegebenen ist anonym erreichbar": Für alle eigenen Controller belegt (Konventionstest + `[Authorize]`). Für die Identity-Minimal-Endpunkte nur teilweise belegt; ein Test über die Endpunktliste fehlt (R-03). Kein Wert gilt daher als "erfüllt", bis dieser Test existiert.

## 4. Secrets-Befund

Ergebnis: Keine echten Secrets im Repository gefunden.

- Getrackte Dateien (ohne Tests, Docs, Migrationen) mit Mustern `password|secret|apikey|token|bearer|connectionstring|Username=`, privaten Schlüsseln und bekannten Token-Präfixen (AWS/GitHub/OpenAI) durchsucht: nur Code-Bezeichner, Kommentare und Platzhalter.
- `CleanArchitecture.Presentation.API/appsettings*.json`, `CleanArchitecture.AppHost/appsettings*.json`: nur Logging, `Cors:AllowedOrigins` und `AllowedHosts`; keine Connection Strings oder Schlüssel.
- Connection String der API kommt über `ConnectionStrings:bloodpressuredb` (Aspire-Umgebungsvariable): `CleanArchitecture.Infrastructure/DependencyInjection.cs:24`. Das Postgres-Passwort wird im AppHost nicht gesetzt (`CleanArchitecture.AppHost/AppHost.cs:3-5`, Aspire erzeugt es); Laufzeit nicht geprüft.
- Syncfusion-Lizenz: Wert nur aus Konfiguration, im Repository nur Platzhalter `"API - Key"` in `CleanArchitecture.Presentation.UI/appsettings.sample.json:10`; die echte `appsettings.json` des Projekts ist ignoriert (`git check-ignore`: `CleanArchitecture.Presentation.UI/.gitignore:1125`). `git log -S` über JSON-Dateien: nur der Platzhalterwert in der Historie. Hinweis: Ein Lizenzschlüssel im WASM-Client ist technisch öffentlich einsehbar; das ist Eigenschaft von Syncfusion/WASM, kein Repository-Leck.
- CI/CD: Anmeldung an GHCR ausschließlich mit `secrets.GITHUB_TOKEN` (`.github/workflows/cd.yml:37-41`); CI "keine Secrets erforderlich" (`ci.yml:9`). Der CI-Postgres-Container nutzt `POSTGRES_HOST_AUTH_METHOD: trust` und ein passwortloses Konto, nur für den kurzlebigen Runner (`ci.yml:80-94`); kein Produktionsbezug, aber kein Vorbild für andere Umgebungen.
- Tests enthalten nur Dummy-Zugangsdaten (`Tests/CleanArchitecture.Tests/ApiFactory.cs:30` `Password=unused`; zufällige Passwörter in `AuthIntegrationTests.cs:19`).
- `.gitignore` deckt `.env*` und `*.pfx` (`.gitignore:198,685-689`); `.dockerignore` schließt `.env`/`secrets.dev.yaml` aus dem Build-Kontext aus (`.dockerignore:3,22`). Es sind keine `.env`, `*.pem`, `*.key`, `*.pfx`, `docker-compose*` oder Keystores getrackt.
- `CleanArchitecture.AppHost.csproj` enthält eine `UserSecretsId` (nur Kennung, kein Secret).
- Nicht geprüft: vollständige Historie mit einem Secret-Scanner; Arbeitsverzeichnis-Dateien außerhalb von Git (untracked: `.claude/settings.local.json`, `.DS_Store` – nicht inhaltlich bewertet).

## 5. Weitere Beobachtungen ohne Risikoeinstufung

- `CleanArchitecture.Presentation.API/Properties/launchSettings.json` enthält nur Entwicklungs-URLs.
- `Presentation.UI/wwwroot/appsettings.json:3` zeigt auf `http://127.0.0.1:5000` (Entwicklung); für Produktion muss die UI-Konfiguration auf HTTPS-API zeigen (Release-Konfiguration des UI-Hostings nicht Teil des Repositorys).
- Es gibt keinen Endpunkt zum Löschen des Benutzerkontos (nur `DELETE /api/v1/profile` für Profildaten, `ProfileController.cs:27`); für DSGVO Art. 17 relevant, außerhalb Epic 1.

## 6. Offene Risiken

Schweregrad: Mittel > Gering > Info. Kein Befund "Hoch" oder "Kritisch".

| ID | Schweregrad | Risiko | Vorschlag | Zuständig |
|----|-------------|--------|-----------|-----------|
| R-01 | Mittel | Passwort- und Token-Policy nur Framework-Standard; Refresh-Token ohne serverseitigen Widerruf, 14 Tage (Annahme Standard) | In `AddIdentityApiEndpoints` `Password.RequiredLength = 12`, und `AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme)` mit expliziter `BearerTokenExpiration` (z. B. 15 Min.) und `RefreshTokenExpiration` (z. B. 7 Tage); Werte per Test (Scenario) absichern. Logout-Revoke ggf. per SecurityStamp-Rotation | backend-agent, sec-agent |
| R-02 | Mittel | Kein strengeres Limit für `/api/auth/*`; IP-Partition ohne `UseForwardedHeaders` hinter Proxy | Eigene Rate-Limit-Policy für Auth-Endpunkte (z. B. 10/min je IP) und `ForwardedHeaders` mit `KnownProxies`/`KnownNetworks` konfigurieren; Test für 429 auf `/api/auth/login` | backend-agent, devops-agent |
| R-03 | Mittel | Kein Test, der alle Endpunkte (inkl. `MapIdentityApi`, Health) gegen die Freigabeliste prüft; Lockout ungetestet; weitere anonyme Identity-Endpunkte (`forgotPassword`, `resetPassword`, `confirmEmail`, `resendConfirmationEmail`) nicht ausdrücklich freigegeben | Test über `EndpointDataSource`, der anonyme Endpunkte gegen die Liste aus Abschnitt 3 abgleicht; Test "5 Fehlversuche führen zu 423/Sperre"; nicht benötigte Identity-Endpunkte per Endpoint-Filter sperren oder ausdrücklich freigeben (po-agent: Scenario ergänzen) | qa-agent, backend-agent, po-agent |
| R-04 | Mittel | Blazor-Tokens in `sessionStorage` (XSS-Folgeschaden, Refresh-Token inklusive) | Kurzfristig: strenge CSP ohne `unsafe-inline` (Inline-Skripte in `index.html` in Datei auslagern), Refresh-Token-Lebensdauer kürzen (R-01). Langfristig: BFF mit HttpOnly-Cookie (ADR) | frontend-agent, architect-agent, sec-agent |
| R-05 | Mittel | Keine Security-Header, kein HSTS | Middleware für `X-Content-Type-Options: nosniff`, `Referrer-Policy`, `Content-Security-Policy` (API: `default-src 'none'; frame-ancestors 'none'`), `UseHsts()` außerhalb Development; beim UI-Hosting passende CSP; Test auf Header | backend-agent, devops-agent |
| R-06 | Gering | Altdaten-Übernahme durch ersten beliebigen Registrierenden; Zählung/Update nicht atomar | Übernahme nur bei bewusst aktivierter Konfiguration (z. B. `Legacy:ClaimOnFirstRegistration`), nach Migration deaktivieren; Ausführung in einer Transaktion mit Sperre | architect-agent, backend-agent |
| R-07 | Gering | Offene Registrierung ohne E-Mail-Bestätigung/MFA; Konto-Enumeration möglich | Entscheidung per ADR: `RequireConfirmedEmail` mit `IEmailSender`, ggf. Registrierungs-Einladung; Enumeration durch generische Antworten reduzieren | architect-agent, sec-agent |
| R-08 | Gering | Blazor ohne `AuthorizeView`/`AuthenticationStateProvider`, Seitenschutz nur über `Session.IsAuthenticated` | `AuthenticationStateProvider` auf Basis von `AuthSession` plus `[Authorize]`-Seiten und `AuthorizeRouteView` einführen (rein clientseitige UX, Daten sind serverseitig geschützt) | frontend-agent |
| R-09 | Gering | Android `allowBackup="true"` | `android:allowBackup="false"` oder Backup-Regeln, die `SecureStorage` ausschließen | frontend-agent |
| R-10 | Gering | `AllowedHosts: *` | Produktive Hostnamen per Umgebungsvariable `AllowedHosts` setzen | devops-agent |
| R-11 | Gering | Actions nicht SHA-gepinnt, `latest`-Tag, keine Signatur/SBOM | Actions per Commit-SHA pinnen (Dependabot für Actions), Image-Tag nach SHA/Version verwenden, optional `cosign`/SBOM | devops-agent |
| R-12 | Gering | Sicherheitsereignisse nicht gezielt protokolliert | Logging/Metrik für Login-Fehler, Lockout, 401/429 (ohne Passwörter/Token), Alarm bei Häufung | backend-agent, devops-agent |
| R-13 | Info | NuGet-Prüfung ist ein Momentbild; Moderate-Funde brechen die CI nicht ab (`ci.yml:63-71`) | Regelmäßig (Dependabot/Renovate) und Moderate-Funde in der CI als Warnung sichtbar halten | devops-agent |

## 7. Bewertung der Acceptance Criteria

- "Keine Secrets im Repository": nach den durchgeführten Prüfungen erfüllt (Einschränkung: kein Secret-Scanner über die gesamte Historie).
- "OWASP-Prüfung durch sec-agent dokumentiert": mit diesem Dokument erfüllt.
- "Kein Endpunkt außer den ausdrücklich freigegebenen ist anonym erreichbar": für Controller belegt, für Identity-Endpunkte offen (R-03). Empfehlung: Kriterium erst nach dem Endpunkt-Test abhaken; die übrigen Risiken (R-01 bis R-05) sind vor einer produktiven Veröffentlichung zu bewerten, blockieren aber nicht das Abhaken der Epic-Scenarios.
- "Alle Scenarios als Tests grün": nicht Gegenstand dieser Prüfung (Tests wurden nicht ausgeführt).

## 8. Umsetzungsstand

| Nr. | Status | Umsetzung |
|---|---|---|
| R-01 | Behoben (teilweise) | Passwort-Mindestlänge 12 (`Auth:PasswordMinLength`), Access-Token 30 Minuten (`Auth:AccessTokenMinutes`), Refresh-Token 7 Tage (`Auth:RefreshTokenDays`) in `Program.cs`; Tests `AuthPolicyTests`. Serverseitiger Widerruf beim Logout bleibt offen. |
| R-02 | Behoben | Eigenes Limit für `/api/auth/*` (`RateLimiting:AuthPermitLimitPerMinute`, Standard 10 je IP und Minute); `ForwardedHeaders` nur mit `ForwardedHeaders:Enabled` und `ForwardedHeaders:KnownProxies`; Test `AuthEndpoints_HaveStricterRateLimit`. |
| R-03 | Behoben | Test `OnlyAllowedEndpoints_AreAnonymous` gleicht alle Endpunkte mit der Freigabeliste ab (die vier Identity-Zusatzendpunkte sind dort ausdrücklich aufgeführt, nicht entfernt); Lockout-Test `Login_AfterFiveFailedAttempts_LocksAccountEvenWithCorrectPassword`. |
| R-04 | Offen | Braucht CSP für das UI-Hosting und ggf. ein ADR (BFF mit HttpOnly-Cookie). |
| R-05 | Behoben für die API | `SecurityHeadersMiddleware` (`nosniff`, `Referrer-Policy`, CSP `default-src 'none'; frame-ancestors 'none'` außerhalb Development) und `UseHsts()`; Test `Responses_CarrySecurityHeaders`. Das UI-Hosting ist nicht abgedeckt. |
| R-06, R-07, R-08 | Offen | Entwurfsentscheidungen (Altdaten-Übernahme, E-Mail-Bestätigung/MFA, `AuthenticationStateProvider`). |
| R-09 | Behoben | `android:allowBackup="false"`. Nicht auf einem Gerät geprüft. |
| R-10 | Offen | Hostnamen sind Betriebskonfiguration (`AllowedHosts` per Umgebungsvariable). |
| R-11 | Teilweise behoben | Alle Actions sind auf Commit-SHA gepinnt (Versionskommentar dahinter). Das Image trägt neben `latest` auch einen SHA-Tag; Signatur und SBOM fehlen weiter. |
| R-12 | Behoben (teilweise) | `SecurityEventLoggingMiddleware` protokolliert 401/403, das Rate-Limit protokolliert 429, jeweils ohne Token oder Nutzerdaten; Test `AccessDenied_IsLogged_WithoutCredentials`. Alarmierung bei Häufung bleibt offen. |
| R-13 | Behoben | `.github/dependabot.yml` für GitHub Actions und NuGet (wöchentlich). |

