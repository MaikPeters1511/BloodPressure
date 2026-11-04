# Change: Typisierter Refit-Client für die BloodPressure-API

> **Status:** in-progress
> Ablage: `UserStories/refit-bloodpressure-client.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-refit-bloodpressure-client.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Konsumenten der API (Integrationstests, später Blazor und MAUI) sprechen die Endpunkte heute per Hand-gebautem `HttpClient` und eigener JSON-Deserialisierung an (`Api.razor` mit hartcodierter URL `https://localhost:5000` und Newtonsoft). Das ist fehleranfällig und driftet von der API weg. Ein einziges Refit-Interface ist die gemeinsame, compilergeprüfte Vertragsdefinition.
- **What Changes:**
  - Neues Projekt `CleanArchitecture.Contracts` mit den öffentlichen Vertragstypen (`BloodPressureDto`, `BloodPressureRequest`) und dem Refit-Interface `IBloodPressureApi`.
  - `BloodPressureDto` wandert von `CleanArchitecture.Application` nach `CleanArchitecture.Contracts`, `BloodPressureRequest` von `Presentation.API/Contracts` ebenfalls (**BREAKING** nur für Namespaces; JSON-Format bleibt unverändert).
  - Extension-Methode `AddBloodPressureApiClient(...)` registriert den Client über `AddRefitClient<T>()` samt Resilience und Service-Discovery.
  - Die Integrationstests in `Tests/` nutzen den Refit-Client gegen die API.
- **Impact:** Layer Application (DTO-Verschiebung), Presentation.API (Request-Typ), neues Projekt Contracts, Tests. **Nicht** Teil dieser Change: Umbau von Blazor-UI und MAUI auf den Client (eigene Change-Spec für `frontend-agent`).

## 2. User Stories
- Als Entwickler möchte ich die BloodPressure-API über ein typisiertes Interface aufrufen, damit Vertragsänderungen beim Kompilieren auffallen.
- Als Entwickler möchte ich Integrationstests mit demselben Client schreiben, den auch die Frontends nutzen, damit Test und Produktion denselben Vertrag prüfen.
- Als Betreiber möchte ich, dass der Client Adresse und Resilience aus der Aspire-Konfiguration bezieht, damit keine URLs im Code stehen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Refit-Interface deckt alle BloodPressure-Endpunkte ab
Das System SHALL ein Interface `IBloodPressureApi` bereitstellen, das `GET /api/BloodPressure`, `GET /api/BloodPressure/{id}`, `POST`, `PUT /{id}` und `DELETE /{id}` mit `CancellationToken` abbildet und `BloodPressureDto` bzw. `BloodPressureRequest` verwendet.

##### Scenario: Alle Messungen abrufen
- **GIVEN** die API enthält eine gespeicherte Messung
- **WHEN** `GetAllAsync` aufgerufen wird
- **THEN** wird eine Liste mit dieser Messung als `BloodPressureDto` zurückgegeben

##### Scenario: Messung anlegen
- **GIVEN** eine gültige `BloodPressureRequest`
- **WHEN** `CreateAsync` aufgerufen wird
- **THEN** liefert die API Status 201 und der Client gibt das `BloodPressureDto` mit vergebener Id zurück

##### Scenario: Messung ändern und löschen
- **GIVEN** eine vorhandene Messung
- **WHEN** `UpdateAsync` bzw. `DeleteAsync` mit ihrer Id aufgerufen wird
- **THEN** antwortet die API mit 204 und der Client wirft keine Ausnahme

#### Requirement: Fehlerantworten sind für den Aufrufer auswertbar
Das System SHALL Fehlerantworten der API (Status 400 mit ProblemDetails, 404) als `ApiException` des Clients bereitstellen, sodass Status und ProblemDetails ohne Stacktrace-Informationen auswertbar sind.

##### Scenario: Ungültiger Wert
- **GIVEN** eine Request mit `Systolisch = 999`
- **WHEN** `CreateAsync` aufgerufen wird
- **THEN** wirft der Client eine `ApiException` mit Status 400 und dem ProblemDetails-Titel „Ungültige Eingabe“

##### Scenario: Unbekannte Id
- **GIVEN** keine Messung mit Id 99
- **WHEN** `GetByIdAsync(99)` aufgerufen wird
- **THEN** wirft der Client eine `ApiException` mit Status 404

#### Requirement: Client-Registrierung per Extension-Methode
Das System SHALL `AddBloodPressureApiClient(this IServiceCollection, Uri baseAddress)` bereitstellen, die `IBloodPressureApi` über `AddRefitClient` registriert. Die Basisadresse MUST aus der Konfiguration stammen (unter Aspire `https+http://api` per Service-Discovery) und DARF NICHT im Code hartcodiert sein. Der Client SHALL den Standard-Resilience-Handler verwenden.

##### Scenario: Auflösung per DI
- **GIVEN** ein ServiceProvider mit `AddBloodPressureApiClient`
- **WHEN** `IBloodPressureApi` aufgelöst wird
- **THEN** wird eine Instanz geliefert, deren `HttpClient.BaseAddress` der konfigurierten Adresse entspricht

#### Requirement: JSON-Vertrag bleibt stabil
Das System SHALL die JSON-Serialisierung (camelCase, `dateTime` als ISO-8601 UTC) unverändert lassen, sodass bestehende Konsumenten weiter funktionieren.

##### Scenario: Round-Trip
- **GIVEN** ein `BloodPressureDto` mit bekannten Werten
- **WHEN** es vom Client gelesen wird, nachdem die API es geliefert hat
- **THEN** sind alle Felder (`id`, `systolisch`, `diastolisch`, `dateTime`, `pulse`, `oxygen`) gleich den gespeicherten Werten

### MODIFIED Requirements
_Entfällt._

### REMOVED Requirements
_Entfällt._

## 4. UI/UX Requirements
Entfällt in dieser Change. Folgearbeit: Umstellung von `Api.razor` (hartcodierte URL, Newtonsoft) und MAUI auf `IBloodPressureApi` durch `frontend-agent` per eigener Spec.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung:** unverändert (API aktuell `[AllowAnonymous]`). Sobald Microsoft Identity eingeführt ist, MUST der Client ein Token per `DelegatingHandler` mitsenden (separate Change, `sec-agent`).
- **Endpoints:** unverändert (`/api/BloodPressure`, GET/POST/PUT/DELETE).
- **Modelle:**
  - `BloodPressureDto(int Id, int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen)`
  - `BloodPressureRequest(int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen)`
  - `IBloodPressureApi` mit `[Get]`, `[Post]`, `[Put]`, `[Delete]`-Attributen.

## 6. KI/AI Requirements
Entfällt.

## 7. Design (Technischer Ansatz)
- **Neues Projekt `CleanArchitecture.Contracts`:** hat keine Abhängigkeit auf Domain, Application oder Infrastructure; nur Paket `Refit`. Application und Presentation.API verweisen darauf (Abhängigkeitsrichtung nach innen bleibt erhalten, Contracts ist reines Vertragsprojekt).
- **Target Frameworks:** `net10.0`. Da die Blazor-UI noch auf .NET 8 liegt, ist bei der späteren Frontend-Umstellung Multi-Targeting (`net8.0;net10.0`) oder ein UI-Upgrade zu klären – Risiko für die Folge-Spec.
- **Registrierung:** `services.AddRefitClient<IBloodPressureApi>().ConfigureHttpClient(c => c.BaseAddress = baseAddress)`; Resilience und Service-Discovery kommen über `ConfigureHttpClientDefaults` aus `CleanArchitecture.ServiceDefaults`.
- **Serialisierung:** `SystemTextJsonContentSerializer` mit Web-Defaults, passend zur API; Newtonsoft wird nicht eingeführt.
- **Integrationstests:** `WebApplicationFactory<Program>` mit `ConfigureTestServices`, das `IBloodPressureRepository` durch `FakeBloodPressureRepository` ersetzt; Refit nutzt `factory.CreateClient()`. Die Npgsql-Registrierung benötigt dafür einen Dummy-Connection-String in der Testkonfiguration; ein Testcontainer ist nicht erforderlich.
- **Alternativen:** manuell geschriebener `HttpClient`-Wrapper (mehr Code, driftet), OpenAPI-Codegen (zusätzlicher Build-Schritt). Refit gewählt wegen Einfachheit und Attribut-basiertem Vertrag. Kein ADR nötig, solange nur dieses Interface betroffen ist.
- **Risiko:** Namespace-Verschiebung der DTOs ändert Usings in Application, API und Tests (kompilergeprüft).

## 8. Tasks (Implementierungs-Checkliste)
- [x] 1. Tests aus den Scenarios schreiben (Red) – Ordner `Tests/` (WebApplicationFactory + Refit)
- [x] 2. `CleanArchitecture.Contracts` anlegen, DTO/Request verschieben, `IBloodPressureApi`-Interface implementieren (Domain → Application → Infrastructure → Web)
- [x] 3. `AddBloodPressureApiClient` ergänzen und Aspire-Service-Discovery prüfen
- [ ] 4. Frontend-Umstellung: **nicht in dieser Change** (Folge-Spec für `frontend-agent`)
- [ ] 5. QA: Integrationstests grün, JSON-Vertrag unverändert (qa-agent)
- [ ] 6. Doku/OpenAPI aktualisiert (doc-agent)

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test (TUnit) abgedeckt und grün.
- [ ] Build ohne Fehler; keine neuen NuGet-Schwachstellenwarnungen durch `Refit`.
- [ ] Kein hartcodierter Host im Client-Code.
- [ ] JSON-Format der API ist feldgleich zum Stand vor der Change (Round-Trip-Test).
- [ ] Spec ist archiviert (Status `archived`, Requirements nach `UserStories/specs/bloodpressure-api.openspec.md` übernommen).
