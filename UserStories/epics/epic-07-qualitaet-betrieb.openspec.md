# Epic 7: Qualität und Robustheit der API

> **Status:** in-progress · **Priorität:** 2 (Postgres-Tests früh) · **Abhängigkeiten:** keine (einzelne Tasks nach Epic 1/2) · **Agenten:** qa-agent, backend-agent, devops-agent
> Ablage: `UserStories/epics/epic-07-qualitaet-betrieb.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Die Tests laufen gegen ein Fake-Repository; Fehler in EF-Mapping, Migrationen und SQL bleiben unentdeckt. Parallele Änderungen überschreiben sich still, und die API ist weder versioniert noch gedrosselt.
- **What Changes:** Integrationstests mit echtem PostgreSQL (Testcontainers), Migrationsprüfung in der CI, Optimistic Concurrency, API-Versionierung, Rate Limiting.
- **Impact:** Tests, CI-Workflow, Domain/Infrastructure (Concurrency-Token, Migration), API-Routen.

## 2. User Stories
- Als Entwickler möchte ich Datenbank-Verhalten automatisch testen, damit Migrationen und Abfragen verlässlich sind.
- Als Nutzer möchte ich nicht unbemerkt Änderungen eines anderen Geräts überschreiben.
- Als Betreiber möchte ich Missbrauch durch Anfrageflut begrenzen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Integrationstests gegen PostgreSQL
Das System SHALL eine Testsuite (`[Category("Integration")]`) besitzen, die in einem PostgreSQL-Testcontainer die Migrationen anwendet und die Repository-Operationen prüft.

##### Scenario: Migrationen anwenden
- **GIVEN** ein leerer PostgreSQL-Container
- **WHEN** alle Migrationen angewendet werden
- **THEN** gelingt das ohne Fehler

##### Scenario: CRUD gegen echte Datenbank
- **GIVEN** migrierte Datenbank
- **WHEN** eine Messung angelegt, geändert und gelöscht wird
- **THEN** stimmen die gelesenen Werte mit den geschriebenen überein

#### Requirement: Modell und Migration stimmen überein
Das System SHALL einen Test enthalten, der fehlschlägt (und damit die CI), wenn das EF-Modell Änderungen enthält, für die keine Migration existiert (`Database.HasPendingModelChanges()`).

##### Scenario: Vergessene Migration
- **GIVEN** eine Modelländerung ohne Migration
- **WHEN** die CI läuft
- **THEN** schlägt der Test fehl

#### Requirement: Optimistic Concurrency
Das System SHALL beim Ändern/Löschen den `If-Match`-Header (ETag aus dem Concurrency-Token `xmin`) auswerten und bei Abweichung mit 412 antworten.

##### Scenario: Veralteter Stand
- **GIVEN** Gerät A und B haben denselben Stand gelesen und A hat geändert
- **WHEN** B mit dem alten ETag ändert
- **THEN** antwortet die API mit 412

#### Requirement: API-Versionierung
Das System SHALL Routen unter `/api/v1/…` anbieten; unversionierte Pfade SHALL auf v1 weiterleiten (308) bis zur Abkündigung.

##### Scenario: Versionierter Pfad
- **GIVEN** die API läuft
- **WHEN** `GET /api/v1/BloodPressure` aufgerufen wird
- **THEN** antwortet sie wie bisher `/api/BloodPressure`

#### Requirement: Rate Limiting
Das System SHALL je Nutzer (oder IP bei anonymen Endpunkten) höchstens 100 Anfragen pro Minute zulassen und darüber mit 429 samt `Retry-After` antworten.

##### Scenario: Limit überschritten
- **GIVEN** 100 Anfragen in einer Minute
- **WHEN** die 101. folgt
- **THEN** antwortet die API mit 429

### MODIFIED Requirements
**CI-Pipeline:** `ci.yml` führt die Integrationstests (Docker auf `ubuntu-latest`) und die Migrationsprüfung bereits über `dotnet test` aus; es ist keine Workflow-Änderung nötig.

### REMOVED Requirements
_Entfällt._

## 4. Design
- Testcontainers (`Testcontainers.PostgreSql`) als TUnit-Fixture mit `[ClassDataSource<…>(Shared = SharedType.PerTestSession)]`; Container-Start 5–10 s.
- Concurrency: Npgsql `xmin` als Token (`IsRowVersion()`), neue Migration ändert keine Spalte.
- Versionierung mit `Asp.Versioning.Mvc`; Rate Limiting mit eingebautem `AddRateLimiter`.
- Reihenfolge: Postgres-Tests und Migrationsprüfung zuerst (unabhängig von anderen Epics).

## 5. Tasks
- [x] T7.1 (qa) Testcontainers-Fixture + Migrations-/CRUD-Tests
- [x] T7.2 (devops) CI: Integrationstests und Modellprüfung laufen über `dotnet test` (kein Workflow-Umbau)
- [ ] T7.3 (backend) Concurrency-Token, ETag/If-Match, Tests
- [ ] T7.4 (backend) Versionierung inkl. Weiterleitung
- [ ] T7.5 (backend) Rate Limiting, Tests mit niedrigem Limit
- [ ] T7.6 (doc) OpenAPI pro Version

## 6. Acceptance Criteria (DoD)
- [ ] Alle Scenarios als Tests grün, lokal und in der CI.
- [ ] CI-Laufzeit steigt um höchstens 3 Minuten.
