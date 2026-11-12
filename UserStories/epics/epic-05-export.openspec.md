# Epic 5: Export für den Arzt

> **Status:** in-progress · **Priorität:** 4 · **Abhängigkeiten:** Epic 1, Epic 2, Epic 4 (Kategorie, Statistik) · **Agenten:** backend-agent
> Ablage: `UserStories/epics/epic-05-export.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Nutzer zeigen ihre Werte beim Arzttermin. Ein Export spart Abschreiben.
- **What Changes:** `GET /api/BloodPressure/export?format=csv|pdf&from&to` liefert eine Datei.
- **Impact:** Application (Export-Query), Infrastructure (CSV-/PDF-Writer), API. Keine Schemaänderung.

## 2. User Stories
- Als Nutzer möchte ich meine Werte für einen Zeitraum als CSV exportieren, damit ich sie in Excel auswerten kann.
- Als Nutzer möchte ich einen PDF-Bericht mit Tabelle und Durchschnitt, damit ich ihn dem Arzt geben kann.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: CSV-Export
Das System SHALL die Messungen des Nutzers im Zeitraum als UTF-8-CSV (Kopfzeile, Trennzeichen `;`, ISO-8601-Datum, Dezimalpunkt) mit `Content-Disposition: attachment` liefern.

##### Scenario: CSV mit zwei Messungen
- **GIVEN** zwei Messungen im Zeitraum
- **WHEN** `format=csv` angefragt wird
- **THEN** besteht die Datei aus Kopfzeile plus zwei Datenzeilen

##### Scenario: Fremde Daten
- **GIVEN** Messungen eines anderen Nutzers
- **WHEN** der Export angefragt wird
- **THEN** enthält er sie nicht

#### Requirement: PDF-Bericht
Das System SHALL für `format=pdf` einen Bericht mit Zeitraum, Messtabelle, Durchschnitt, Minimum/Maximum und Haftungshinweis liefern.

##### Scenario: PDF abrufen
- **GIVEN** Messungen im Zeitraum
- **WHEN** `format=pdf` angefragt wird
- **THEN** ist `Content-Type` `application/pdf` und die Datei beginnt mit `%PDF`

#### Requirement: Grenzen
Das System SHALL höchstens 10 000 Messungen je Export liefern und bei Überschreitung mit 400 antworten.

##### Scenario: Zu großer Zeitraum
- **GIVEN** mehr als 10 000 Messungen im Zeitraum
- **WHEN** exportiert wird
- **THEN** 400 mit Hinweis, den Zeitraum zu verkleinern

### MODIFIED Requirements
_Entfällt._

### REMOVED Requirements
_Entfällt._

## 4. Design
- Die Exportdatei entsteht im Speicher; durch die Obergrenze von 10 000 Messungen bleibt sie klein (gemessen: CSV 457 KB in 2 ms, PDF 669 KB in 0,5 s). PDF mit QuestPDF, Lizenzentscheidung in `docs/adr/0002-pdf-export-questpdf.md`.
- Kein Zwischenspeichern von Berichten (keine Daten auf Platte).
- Sicherheit: nur eigene Daten (Epic 1), Dateiname ohne Nutzereingaben.

## 5. Tasks
- [x] T5.1 (architect) Lizenz-/Bibliotheksentscheidung PDF (ADR)
- [x] T5.2 (backend) Tests aus den Scenarios (Red)
- [x] T5.3 (backend) CSV-Writer, PDF-Writer, Endpunkt
- [ ] T5.4 (qa) PDF-Smoke (Öffnen, Seitenzahl), CSV in Excel/LibreOffice (offen: nur automatisierte Prüfung `%PDF`/Zeilen, keine manuelle Sichtprüfung)

## 6. Acceptance Criteria (DoD)
- [x] Alle Scenarios als Tests grün (Gesamtsuite inkl. PostgreSQL-Tests).
- [x] Export von 10 000 Zeilen ohne nennenswerten Speicheranstieg (Dateien unter 1 MB, Laufzeit unter 1 s).
