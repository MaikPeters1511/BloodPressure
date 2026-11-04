# Epic 3: Validierung und Fehlerantworten

> **Status:** proposed · **Priorität:** 2 · **Abhängigkeiten:** keine · **Agenten:** backend-agent, qa-agent
> Ablage: `UserStories/epics/epic-03-validierung-fehler.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Aktuell meldet die API nur die erste verletzte Domain-Regel als 400 und prüft keine Beziehungen zwischen Feldern (z. B. Systolisch ≥ Diastolisch). Zudem protokolliert Wolverine jede Validierung als `fail`.
- **What Changes:** Request-Validierung vor dem Handler (FluentValidation), Fehlerantwort als `ValidationProblemDetails` mit allen Feldfehlern, zusätzliche Fachregeln.
- **Impact:** Application (Validatoren), API (Fehlerabbildung), Contracts (Fehlerformat), Tests.

## 2. User Stories
- Als Nutzer möchte ich alle Eingabefehler auf einmal sehen, damit ich sie in einem Schritt korrigieren kann.
- Als Betreiber möchte ich Validierungsfehler nicht als Serverfehler im Log sehen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Alle Feldfehler gemeinsam melden
Das System SHALL bei ungültigen Requests mit Status 400 und `ValidationProblemDetails` antworten, das jeden fehlerhaften Feldnamen mit Meldung enthält.

##### Scenario: Mehrere Fehler
- **GIVEN** eine Request mit `Systolisch = 999` und `Oxygen = 150`
- **WHEN** sie gesendet wird
- **THEN** enthält `errors` Einträge für `systolisch` und `oxygen`

#### Requirement: Fachliche Konsistenzregeln
Das System SHALL ablehnen: Systolisch kleiner oder gleich Diastolisch, Messzeitpunkt in der Zukunft (Toleranz 5 Minuten).

##### Scenario: Systolisch unter Diastolisch
- **GIVEN** `Systolisch = 70`, `Diastolisch = 90`
- **WHEN** die Messung angelegt wird
- **THEN** antwortet die API mit 400 und einem Fehler am Feld `systolisch`

##### Scenario: Zukünftiger Zeitpunkt
- **GIVEN** ein `DateTime` eine Stunde in der Zukunft
- **WHEN** die Messung angelegt wird
- **THEN** antwortet die API mit 400 am Feld `dateTime`

#### Requirement: Validierungsfehler sind kein Serverfehler
Das System SHALL Validierungsfehler nicht als Fehler (`Error`) protokollieren.

##### Scenario: Log-Level
- **GIVEN** eine ungültige Request
- **WHEN** sie verarbeitet wird
- **THEN** erscheint kein Logeintrag mit Level Error oder höher

### MODIFIED Requirements
**Fehlerantwort bei ungültiger Eingabe:** Statt `ProblemDetails` mit dem Text der ersten Exception liefert die API `ValidationProblemDetails`. Die Domain-Setter behalten ihre Invarianten als letzte Verteidigungslinie.

### REMOVED Requirements
_Entfällt._

## 4. Design
- Validatoren (FluentValidation) in `Application`, über Wolverine-Middleware vor dem Handler; Domain-Exceptions bleiben als Fallback im `GlobalExceptionHandler`.
- Fehlerformat bleibt RFC 9457; Feldnamen camelCase wie das JSON.
- Risiko: Doppelte Regeln (Validator und Domain) – Grenzwerte als Konstanten in der Domain teilen.

## 5. Tasks
- [ ] T3.1 (backend) Tests aus den Scenarios (Red)
- [ ] T3.2 (backend) Validatoren + Wolverine-Middleware
- [ ] T3.3 (backend) `GlobalExceptionHandler` / Logging anpassen
- [ ] T3.4 (backend) Refit: `ValidationProblemDetails` im Test-Client auswertbar
- [ ] T3.5 (doc) OpenAPI-Beispiele für Fehler

## 6. Acceptance Criteria (DoD)
- [ ] Alle Scenarios als Tests grün.
- [ ] Keine Stacktraces in Antworten.
- [ ] Grenzwerte stehen nur an einer Stelle.
