# Epic 2: Abfrage mit Paging, Filter und Sortierung

> **Status:** proposed · **Priorität:** 2 · **Abhängigkeiten:** (optional) Epic 1 für Benutzerfilter · **Agenten:** backend-agent, frontend-agent (Folge)
> Ablage: `UserStories/epics/epic-02-abfrage-paging-filter.openspec.md`

## 1. Proposal (Why & What)
- **Why:** `GET /api/BloodPressure` lädt heute die gesamte Tabelle. Bei vielen Messungen wächst Antwortzeit und Speicherbedarf unbegrenzt.
- **What Changes:** Zeitraumfilter, Paging und Sortierung. **BREAKING:** Die Listenantwort wird von einem JSON-Array zu einem Envelope `{ items, page, pageSize, totalCount }`.
- **Impact:** Repository, Handler, Controller, `IBloodPressureApi`, `Api.razor` (Folgearbeit).

## 2. User Stories
- Als Nutzer möchte ich meine Werte für einen Zeitraum abrufen, damit ich nur Relevantes sehe.
- Als Nutzer möchte ich seitenweise blättern, damit lange Listen schnell laden.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Zeitraumfilter
Das System SHALL `from` und `to` (ISO-8601, UTC, inklusiv) als optionale Query-Parameter akzeptieren.

##### Scenario: Eingegrenzter Zeitraum
- **GIVEN** Messungen am 1., 5. und 10. November
- **WHEN** `from=2026-11-04&to=2026-11-06` übergeben wird
- **THEN** enthält das Ergebnis nur die Messung vom 5. November

##### Scenario: Ungültiger Zeitraum
- **GIVEN** `from` liegt nach `to`
- **WHEN** die Liste abgefragt wird
- **THEN** antwortet die API mit 400 und ProblemDetails

#### Requirement: Paging und Sortierung
Das System SHALL `page` (ab 1), `pageSize` (Standard 50, Maximum 200) und `sort` (`dateTime` oder `-dateTime`, Standard `-dateTime`) unterstützen und `totalCount` mitliefern.

##### Scenario: Zweite Seite
- **GIVEN** 120 Messungen
- **WHEN** `page=2&pageSize=50` angefragt wird
- **THEN** enthält `items` 50 Einträge und `totalCount` ist 120

##### Scenario: Zu große Seite
- **GIVEN** `pageSize=1000`
- **WHEN** die Liste angefragt wird
- **THEN** wird `pageSize` auf 200 begrenzt (oder mit 400 abgelehnt – siehe Design)

### MODIFIED Requirements
**Liste der Messungen:** Das System SHALL `GET /api/BloodPressure` als paginierte, nach Datum sortierte Liste im Envelope-Format liefern (vorher: vollständiges Array).

### REMOVED Requirements
_Entfällt._

## 4. Design
- Datenbankseitige Filterung/Paging (`Where`/`Skip`/`Take`), Index auf `(UserId, DateTime)` per neuer Migration; `AsNoTracking` bleibt.
- Neue Typen `PagedResult<T>` und `BloodPressureQuery` in `Contracts`.
- Entscheidung: zu große `pageSize` → begrenzen (freundlicher) statt 400.
- Risiko: Breaking Change für Konsumenten; Hinweis in OpenAPI, Versionierung siehe Epic 7.

## 5. Tasks
- [ ] T2.1 (backend) Tests für Filter, Paging, Sortierung, Grenzwerte (Red)
- [ ] T2.2 (backend) Repository- und Handler-Erweiterung, Migration (Index)
- [ ] T2.3 (backend) Controller + `IBloodPressureApi` anpassen
- [ ] T2.4 (qa) Performance-Smoke mit 100 000 Zeilen
- [ ] T2.5 (frontend, Folge-Spec) `Api.razor` auf Paging umstellen

## 6. Acceptance Criteria (DoD)
- [ ] Alle Scenarios als Tests grün.
- [ ] Abfrage nutzt den Index (EXPLAIN geprüft), Antwort bei 100 000 Zeilen unter 200 ms (lokal).
- [ ] OpenAPI zeigt Parameter und Envelope.
