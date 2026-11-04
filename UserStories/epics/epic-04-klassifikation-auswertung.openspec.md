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
Das System SHALL jede Messung nach ESC/ESH 2018 einordnen: Optimal (<120 und <80), Normal (120–129 und/oder 80–84), Hochnormal (130–139 und/oder 85–89), Hypertonie Grad 1 (140–159 und/oder 90–99), Grad 2 (160–179 und/oder 100–109), Grad 3 (≥180 und/oder ≥110). Bei verschiedenen Kategorien von Systolisch und Diastolisch gilt die höhere. Die **isolierte systolische Hypertonie** (systolisch ≥ 140 bei diastolisch < 90) ist ein Zusatzmerkmal (`IsIsolatedSystolic` im Domain-Ergebnis), keine eigene Kategorie; z. B. ist 140/85 „Grad 1“ mit gesetztem Merkmal.

##### Scenario: Optimaler Wert
- **GIVEN** 118/76
- **WHEN** die Kategorie berechnet wird
- **THEN** ist sie „Optimal“

##### Scenario: Gemischte Kategorien
- **GIVEN** 132/92
- **WHEN** die Kategorie berechnet wird
- **THEN** ist sie „Hypertonie Grad 1“ (Diastolisch bestimmt)

##### Scenario: Grenzwert
- **GIVEN** 140/85
- **WHEN** die Kategorie berechnet wird
- **THEN** ist sie „Hypertonie Grad 1“

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
- [ ] T4.1 (architect) Schwellenwerte mit Quelle bestätigen (offen: Umsetzung folgt ESC/ESH 2018 aus dem Gedächtnis, nicht gegen die Leitlinie geprüft)
- [x] T4.2 (backend) Parametrisierte Tests für Kategorien und Statistik (Red)
- [x] T4.3 (backend) Domain-Klassifikation, DTO-Erweiterung
- [x] T4.4 (backend) Statistik-Query, Endpunkt, Refit-Methode
- [x] T4.5 (doc) OpenAPI-Beschreibung mit Haftungshinweis an den Endpunkten

## 6. Acceptance Criteria (DoD)
- [ ] Alle Grenzwerte (je ±1) sind durch Tests abgedeckt und grün.
- [ ] Statistik stimmt mit manueller Berechnung überein (Teststichprobe).
- [ ] Haftungshinweis in der API-Doku.
