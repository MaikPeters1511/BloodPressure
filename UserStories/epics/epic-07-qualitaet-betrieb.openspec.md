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
Das System SHALL `GET /api/v1/BloodPressure/{id}` mit einem `ETag` (Concurrency-Token `xmin`) ausliefern und beim Ändern/Löschen einen vorhandenen `If-Match`-Header auswerten; bei Abweichung antwortet es mit 412. Fehlt der Header oder ist er `*`, wird nicht geprüft (Rückwärtskompatibilität); ein nicht lesbarer Wert führt zu 400. Ein Konflikt, der erst beim Speichern auffällt (Wettlauf), führt zu 409.

##### Scenario: Veralteter Stand
- **GIVEN** Gerät A und B haben denselben Stand gelesen und A hat geändert
- **WHEN** B mit dem alten ETag ändert
- **THEN** antwortet die API mit 412

#### Requirement: API-Versionierung
Das System SHALL die API-Controller unter `/api/v1/…` anbieten; die bisherigen unversionierten Pfade (`/api/BloodPressure`, `/api/reminders`, `/api/notifications`) SHALL dauerhaft auf v1 weiterleiten (308, Methode und Query bleiben erhalten) bis zur Abkündigung. Die Identity-Endpunkte unter `/api/auth` sind Teil des Frameworks und bleiben unversioniert.

##### Scenario: Versionierter Pfad
- **GIVEN** die API läuft
- **WHEN** `GET /api/v1/BloodPressure` aufgerufen wird
- **THEN** antwortet sie wie bisher `/api/BloodPressure`

#### Requirement: Rate Limiting
Das System SHALL je Nutzer (oder IP bei anonymen Anfragen) höchstens 100 Anfragen pro Minute zulassen (konfigurierbar über `RateLimiting:PermitLimitPerMinute`) und darüber mit 429 samt `Retry-After` antworten. Die Health-Probes `/alive` und `/health` sind ausgenommen.

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
- Concurrency: Npgsql `xmin` als Token (`IsRowVersion()`); die Migration legt keine echte Spalte an (Systemspalte). Das Repository übersetzt `DbUpdateConcurrencyException` in eine Domain-Ausnahme (409), die Version wird zusätzlich im Handler gegen `If-Match` geprüft (412).
- Versionierung: bewusst ohne `Asp.Versioning`-Bibliothek, mit festen Routen `api/v1/...` und einer Weiterleitung für die alten Pfade (eine zweite Version bekommt später neue Controller). Rate Limiting mit eingebautem `AddRateLimiter` (Fixed Window, Partition je Nutzer bzw. IP).
- Hinter einem Reverse Proxy muss `ForwardedHeaders` konfiguriert werden, damit die Client-IP für anonyme Anfragen stimmt (nicht Teil dieser Change).
- Reihenfolge: Postgres-Tests und Migrationsprüfung zuerst (unabhängig von anderen Epics).

## 5. Tasks
- [x] T7.1 (qa) Testcontainers-Fixture + Migrations-/CRUD-Tests
- [x] T7.2 (devops) CI: Integrationstests und Modellprüfung laufen über `dotnet test` (kein Workflow-Umbau)
- [x] T7.3 (backend) Concurrency-Token, ETag/If-Match, Tests
- [x] T7.4 (backend) Versionierung inkl. Weiterleitung
- [x] T7.5 (backend) Rate Limiting, Tests mit niedrigem Limit
- [ ] T7.6 (doc) OpenAPI pro Version (offen: es gibt ein OpenAPI-Dokument mit den v1-Routen; getrennte Dokumente je Version erst mit einer zweiten Version)

## 6. Acceptance Criteria (DoD)
- [x] Alle Scenarios als Tests grün, lokal (138 Tests); der CI-Lauf folgt im PR.
- [ ] CI-Laufzeit steigt um höchstens 3 Minuten (wird am PR-Lauf geprüft).
