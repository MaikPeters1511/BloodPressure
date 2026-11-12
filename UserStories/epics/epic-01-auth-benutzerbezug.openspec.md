# Epic 1: Authentifizierung und Benutzerbezug

> **Status:** in-progress · **Priorität:** 1 (Grundlage) · **Abhängigkeiten:** keine · **Agenten:** sec-agent, backend-agent, architect-agent
> Ablage: `UserStories/epics/epic-01-auth-benutzerbezug.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Blutdruckwerte sind Gesundheitsdaten (DSGVO Art. 9). Die API ist aktuell `[AllowAnonymous]`, alle Messungen sind für jeden les- und löschbar. `CLAUDE.md` verlangt abgesicherte Endpunkte als Standard.
- **What Changes:** Anmeldung über Microsoft Identity mit Bearer-Token, Benutzerbezug an jeder Messung, Zugriff nur auf eigene Daten. **BREAKING:** alle `/api/BloodPressure`-Endpunkte verlangen ein Token.
- **Impact:** Domain (`UserId` an `BloodPressure`), Infrastructure (Identity-Store in PostgreSQL, neue Migration), API, Contracts/Refit-Client (Token-Handler), spätere Frontends.

## 2. User Stories
- Als Nutzer möchte ich mich registrieren und anmelden, damit nur ich meine Werte sehe.
- Als Nutzer möchte ich, dass andere meine Messungen weder lesen noch ändern können.
- Als Betreiber möchte ich keine Secrets im Code, damit Schlüssel über Konfiguration/Secret Manager kommen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Authentifizierung per Bearer-Token
Das System SHALL Registrierung, Login und Token-Refresh über Microsoft Identity (ASP.NET Core Identity API, PostgreSQL-Store) anbieten. Jeder Controller SHALL `[Authorize]` oder `[AllowAnonymous]` explizit deklarieren (durch einen Test erzwungen); Ausnahmen (`/health`, Login, Registrierung) sind anonym. Eine globale Fallback-Policy wird bewusst nicht verwendet, da sie die Identity-Endpunkte (Registrierung/Login) mit sperren würde.

##### Scenario: Anfrage ohne Token
- **GIVEN** kein Authorization-Header
- **WHEN** `GET /api/BloodPressure` aufgerufen wird
- **THEN** antwortet die API mit 401

##### Scenario: Login
- **GIVEN** ein registrierter Nutzer
- **WHEN** er sich mit gültigen Zugangsdaten anmeldet
- **THEN** erhält er ein Access- und ein Refresh-Token

#### Requirement: Daten gehören einem Benutzer
Das System SHALL jede Messung mit der `UserId` des angemeldeten Nutzers speichern und bei allen Lese-, Änderungs- und Löschzugriffen auf die eigene `UserId` filtern.

##### Scenario: Fremde Messung lesen
- **GIVEN** Nutzer A besitzt Messung 1
- **WHEN** Nutzer B `GET /api/BloodPressure/1` aufruft
- **THEN** antwortet die API mit 404 (Existenz wird nicht verraten)

##### Scenario: Eigene Liste
- **GIVEN** Nutzer A und B haben je eine Messung
- **WHEN** Nutzer A die Liste abruft
- **THEN** enthält sie nur seine eigene Messung

#### Requirement: Bestehende Daten bleiben nutzbar
Das System SHALL die Migration so ausführen, dass vorhandene Messungen ohne Datenverlust erhalten bleiben (`UserId` = `null`) und unmittelbar nach der Registrierung des ersten Nutzers diesem zugeordnet werden. Gibt es bereits mehrere Nutzer, bleiben sie ohne Besitzer und sind nicht abrufbar.

##### Scenario: Migration auf gefüllter Datenbank
- **GIVEN** eine Datenbank mit Messungen ohne `UserId`
- **WHEN** die Migration angewendet wird
- **THEN** schlägt sie nicht fehl und die Messungen bleiben erhalten (ohne Besitzer)

##### Scenario: Erster Nutzer übernimmt Altdaten
- **GIVEN** Messungen ohne Besitzer und noch kein Benutzer
- **WHEN** sich der erste Nutzer registriert
- **THEN** gehören die Altdaten ihm; ein später registrierter zweiter Nutzer sieht sie nicht

### MODIFIED Requirements
_`IBloodPressureApi` (Refit) SHALL ein Token über einen `DelegatingHandler` mitsenden; Fehler 401 wird als `ApiException` gemeldet._

### REMOVED Requirements
`[AllowAnonymous]` am `BloodPressureController` entfällt.

## 4. Design (Technischer Ansatz)
- ASP.NET Core Identity mit `MapIdentityApi<TUser>()` und Bearer-Tokens (passt für Blazor WASM und MAUI ohne Cookies); Alternative: externer IdP (Keycloak/Entra ID) – Entscheidung per ADR in `docs/adr/`.
- `UserId` aus dem Claim `sub`/`NameIdentifier`; Wolverine-Messages tragen die UserId explizit (kein HttpContext in Handlern).
- Risiken: Brute-Force (Lockout aktivieren), Token-Lebensdauer, Passwort-Policy.
- **Entschieden:** Altdaten gehören dem ersten registrierten Nutzer (Zuordnung direkt nach der Registrierung über einen Endpoint-Filter).

## 5. Tasks
- [x] T1.1 (architect) ADR: Identity-API vs. externer IdP (`docs/adr/0001-identity-api.md`)
- [x] T1.2 (backend) Tests aus den Scenarios (TUnit, `WebApplicationFactory` mit Testtoken) – Red
- [x] T1.3 (backend) `UserId` in Domain/Repository, Filter in Handlern, Migration mit Altdaten-Strategie
- [x] T1.4 (sec) Identity einrichten, Fallback-Policy, Lockout, Secrets nur aus Konfiguration
- [x] T1.5 (backend) Refit-`DelegatingHandler` für Token in `Contracts`
- [x] T1.6 (qa) Zugriffstests (401/404, Mandantentrennung)
- [x] T1.7 (doc) OpenAPI mit Security Scheme

## 6. Acceptance Criteria (DoD)
- [ ] Alle Scenarios als Tests grün; kein Endpunkt außer den ausdrücklich freigegebenen ist anonym erreichbar.
- [x] Keine Secrets im Repository; OWASP-Prüfung durch `sec-agent` dokumentiert: [docs/security/owasp-epic-01.md](../../docs/security/owasp-epic-01.md) (statische Prüfung, keine Funde mit hohem Schweregrad; offene mittlere Risiken R-01 bis R-05 dort beschrieben).
- [ ] Spec archiviert und in `UserStories/specs/` übernommen.
