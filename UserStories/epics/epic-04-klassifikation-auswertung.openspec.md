# Epic 4: Klassifikation und Auswertung

> **Status:** in-progress · **Priorität:** 3 · **Abhängigkeiten:** Epic 1 (Benutzerfilter), Epic 2 (Zeitraumfilter) · **Agenten:** backend-agent, architect-agent
> Ablage: `UserStories/epics/epic-04-klassifikation-auswertung.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Einzelne Zahlen sagen dem Nutzer wenig. Einordnung und Verlauf sind der eigentliche Nutzen einer Blutdruck-App.
- **What Changes:** Eine Domänenlogik ordnet jede Messung einer Kategorie zu; ein Statistik-Endpunkt liefert Durchschnitt, Minimum, Maximum und Anzahl je Zeitabschnitt.
- **Impact:** Domain (Value Object `BloodPressureCategory`), Application (Query), API, Contracts (neue DTOs). Keine Schemaänderung (Kategorie wird berechnet, nicht gespeichert).
- **Hinweis:** Medizinische Einordnung, kein Diagnoseersatz. Schwellenwerte vor Umsetzung durch eine Fachquelle bestätigen.

## 2. User Stories
- Als Nutzer möchte ich sehen, in welche Kategorie eine Messung fällt, damit ich sie einordnen kann.
- Als Nutzer möchte ich Durchschnittswerte pro Tag, Woche oder Monat sehen, damit ich Trends erkenne.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Kategorie einer Messung
Das System SHALL jede Messung nach den ESC-Leitlinien 2024 (Praxiswerte) einordnen: **Nicht erhöht** (unter 120 und unter 70 mmHg), **Erhöht** (120–139 und/oder 70–89 mmHg), **Bluthochdruck** (ab 140 und/oder ab 90 mmHg). Die Leitlinie kennt keine Schweregrade 1–3 mehr. Bei verschiedenen Kategorien von Systolisch und Diastolisch gilt die höhere. Die **isolierte systolische Hypertonie** (systolisch ≥ 140 bei diastolisch < 90) bleibt ein Zusatzmerkmal (`IsIsolatedSystolic`), keine eigene Kategorie. Unabhängig von der Kategorie gilt eine Messung ab 180 systolisch oder 110 diastolisch als **stark erhöht** (`IsSevere`) und löst den Hinweis aus. Die API-Werte der Kategorie sind `NonElevated`, `Elevated`, `Hypertension`.

##### Scenario: Nicht erhöhter Wert
- **GIVEN** 118/68
- **WHEN** die Kategorie berechnet wird
- **THEN** ist sie „Nicht erhöht“

##### Scenario: Gemischte Kategorien
- **GIVEN** 118/95
- **WHEN** die Kategorie berechnet wird
- **THEN** ist sie „Bluthochdruck“ (Diastolisch bestimmt)

##### Scenario: Grenzwert
- **GIVEN** 119/70 bzw. 140/85
- **WHEN** die Kategorie berechnet wird
- **THEN** ist sie „Erhöht“ bzw. „Bluthochdruck“

#### Requirement: Statistik-Endpunkt
Das System SHALL `GET /api/BloodPressure/statistics?from&to&interval=day|week|month` bereitstellen, der je Zeitabschnitt Anzahl, Durchschnitt, Minimum und Maximum von Systolisch, Diastolisch und Puls liefert; Abschnitte ohne Messung werden ausgelassen.

##### Scenario: Tagesdurchschnitt
- **GIVEN** zwei Messungen am selben Tag (120/80 und 130/90)
- **WHEN** `interval=day` abgefragt wird
- **THEN** liefert der Abschnitt Durchschnitt 125/85, Minimum 120/80, Maximum 130/90, Anzahl 2

##### Scenario: Leerer Zeitraum
- **GIVEN** keine Messungen im Zeitraum
- **WHEN** die Statistik abgefragt wird
- **THEN** ist die Liste leer und der Status 200

### MODIFIED Requirements
**Messungs-DTO:** `BloodPressureDto` SHALL zusätzlich `Category` enthalten.

### REMOVED Requirements
_Entfällt._

## 4. Design
- Kategorie als reiner Domain-Service/Value Object ohne Infrastruktur (leicht testbar, parametrisierte TUnit-Tests mit `[Arguments]`).
- Aggregation in PostgreSQL pro UTC-Tag (`GROUP BY date_trunc('day')` mit Summe/Min/Max/Anzahl), Zusammenfassung zu Woche (Montag) und Monat in der Application aus den Tagesaggregaten (Mittelwerte aus Summen/Anzahl, daher exakt). Zeitzone: UTC.
- Risiko: Zeitzone des Nutzers (Tagesgrenze) – eigene Entscheidung, hier UTC.

## 5. Tasks
- [x] T4.1 (architect) Schwellenwerte: seit 2026-11-05 nach ESC 2024 (ADR 0005); Grenzen aus Sekundärquellen (ESC-Pressemitteilung, Fachpresse) bestätigt, **nicht** gegen den Leitlinientext (Eur Heart J, doi 10.1093/eurheartj/ehae178) geprüft
- [x] T4.2 (backend) Parametrisierte Tests für Kategorien und Statistik (Red)
- [x] T4.3 (backend) Domain-Klassifikation, DTO-Erweiterung
- [x] T4.4 (backend) Statistik-Query, Endpunkt, Refit-Methode
- [x] T4.5 (doc) OpenAPI-Beschreibung mit Haftungshinweis an den Endpunkten

## 6. Acceptance Criteria (DoD)
- [ ] Alle Grenzwerte (je ±1) sind durch Tests abgedeckt und grün.
- [ ] Statistik stimmt mit manueller Berechnung überein (Teststichprobe).
- [x] Haftungshinweis in der API-Doku (Test `OpenApiProblemExamplesTests.ClassifyingOperation_HasDisclaimer`).
