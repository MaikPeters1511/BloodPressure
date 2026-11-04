# Epic 9: KI-Zusammenfassung der Verläufe

> **Status:** proposed · **Priorität:** 6 (zuletzt) · **Abhängigkeiten:** Epic 1, Epic 4 · **Agenten:** ai-agent, sec-agent, backend-agent
> Ablage: `UserStories/epics/epic-09-ki-zusammenfassung.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Eine verständliche Zusammenfassung („Dein Durchschnitt stieg in den letzten zwei Wochen“) macht Verläufe zugänglich.
- **What Changes:** `POST /api/BloodPressure/insights` erzeugt per LLM einen Kurztext aus **aggregierten** Werten.
- **Impact:** Application (Use Case), Infrastructure (LLM-Client), Konfiguration, Einwilligung des Nutzers.
- **Hinweis:** Kein medizinischer Rat. Der Text enthält stets einen Haftungshinweis.

## 2. User Stories
- Als Nutzer möchte ich eine kurze Zusammenfassung meines Verlaufs, damit ich Trends schneller verstehe.
- Als Nutzer möchte ich entscheiden, ob meine Daten dafür an einen Dienst gehen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Nur aggregierte, pseudonyme Daten im Prompt
Das System SHALL dem Modell ausschließlich Kennzahlen aus der Statistik (Epic 4) übergeben – keine `UserId`, keine Namen, keine Einzelzeitpunkte außer Datumsbereich – und dies im Test nachweisen.

##### Scenario: Prompt-Inhalt
- **GIVEN** ein Nutzer mit Messungen
- **WHEN** die Zusammenfassung angefordert wird
- **THEN** enthält der an das Modell gesendete Prompt weder UserId noch E-Mail noch Rohdatensätze

#### Requirement: Einwilligung
Das System SHALL die Funktion nur ausführen, wenn der Nutzer ihr ausdrücklich zugestimmt hat (Widerruf jederzeit).

##### Scenario: Ohne Einwilligung
- **GIVEN** keine Einwilligung
- **WHEN** `POST …/insights` aufgerufen wird
- **THEN** antwortet die API mit 403 und ruft das Modell nicht auf

#### Requirement: Resilienz
Das System SHALL Aufrufe mit Timeout, Retry (exponentielles Backoff) und Circuit Breaker absichern und bei Ausfall mit 503 und sauberer Meldung antworten.

##### Scenario: Modell nicht erreichbar
- **GIVEN** der Modelldienst antwortet nicht
- **WHEN** die Zusammenfassung angefordert wird
- **THEN** antwortet die API nach definierter Zeit mit 503 ohne interne Details

#### Requirement: Austauschbarer Anbieter
Das System SHALL den Anbieter über ein Application-Interface `IInsightGenerator` kapseln; Standard ist ein lokales Modell (LMStudio), OpenAI ist per Konfiguration wählbar. Schlüssel kommen nur aus Konfiguration/Secret Manager.

##### Scenario: Anbieterwechsel
- **GIVEN** Konfiguration auf OpenAI
- **WHEN** die Anwendung startet
- **THEN** wird die OpenAI-Implementierung verwendet, ohne Codeänderung

### MODIFIED Requirements
_Entfällt._

### REMOVED Requirements
_Entfällt._

## 4. Design
- Prompt-Vorlage versioniert im Code; Ausgabe auf feste Länge begrenzt; Antwort wird auf Verbote geprüft („Diagnose“, Medikamentenempfehlung) bevor sie ausgeliefert wird.
- Tests ersetzen das Modell durch einen Fake; ein optionaler Test gegen LMStudio läuft nur lokal (`[Category("Manual")]`).
- Risiko: Halluzination, Datenschutz bei OpenAI (Auftragsverarbeitung, Drittland) – Entscheidung per ADR.

## 5. Tasks
- [ ] T9.1 (architect/sec) ADR: Anbieter, Datenschutz, Einwilligungsspeicherung
- [ ] T9.2 (ai) Tests (Red), `IInsightGenerator`, Prompt, Ausgabefilter
- [ ] T9.3 (ai) LMStudio- und OpenAI-Implementierung mit Polly
- [ ] T9.4 (backend) Endpunkt, Einwilligung (Migration)
- [ ] T9.5 (qa) Prompt-Inhaltstest, Ausfalltests

## 6. Acceptance Criteria (DoD)
- [ ] Alle Scenarios als Tests grün.
- [ ] Prompt-Inhalt automatisch geprüft; Haftungshinweis in jeder Antwort.
- [ ] Keine Schlüssel im Repository.
