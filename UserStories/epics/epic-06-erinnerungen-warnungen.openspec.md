# Epic 6: Erinnerungen und Warnungen

> **Status:** in-progress · **Priorität:** 5 · **Abhängigkeiten:** Epic 1, Epic 4 · **Agenten:** backend-agent, architect-agent, frontend-agent (Anzeige/Push)
> Ablage: `UserStories/epics/epic-06-erinnerungen-warnungen.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Regelmäßiges Messen ist für die Aussagekraft entscheidend; kritische Werte sollen nicht unbemerkt bleiben.
- **What Changes:** Domain Event bei kritischem Wert, planbare Messerinnerungen über Wolverine, Benachrichtigungsliste per API. Push-Zustellung (FCM/APNs) ist eine Folge-Change.
- **Impact:** Domain (Event), Application (Handler, Scheduler), Infrastructure (Persistenz der Benachrichtigungen, neue Migration), API.

## 2. User Stories
- Als Nutzer möchte ich bei einem kritischen Wert (Grad 3) einen Hinweis erhalten, damit ich reagieren kann.
- Als Nutzer möchte ich tägliche Erinnerungen zu festen Uhrzeiten, damit ich das Messen nicht vergesse.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Warnung bei kritischem Wert
Das System SHALL beim Anlegen einer Messung der Kategorie „Hypertonie Grad 3“ in derselben Datenbanktransaktion eine Benachrichtigung für den Nutzer speichern (kein separates asynchrones Event; siehe ADR 0003). Der Hinweis SHALL einen Haftungshinweis enthalten und keine Diagnose stellen.

##### Scenario: Kritischer Wert
- **GIVEN** ein angemeldeter Nutzer
- **WHEN** er 185/115 speichert
- **THEN** existiert danach genau eine ungelesene Benachrichtigung vom Typ „kritischer Wert“

##### Scenario: Normaler Wert
- **GIVEN** ein angemeldeter Nutzer
- **WHEN** er 118/76 speichert
- **THEN** wird keine Benachrichtigung erzeugt

#### Requirement: Messerinnerungen
Das System SHALL pro Nutzer bis zu 5 tägliche Erinnerungszeiten (lokale Uhrzeit mit IANA-Zeitzone) verwalten (`GET/PUT /api/reminders`) und zur Zeit eine Benachrichtigung erzeugen, sofern an diesem Tag noch keine Messung nach der letzten Erinnerung vorliegt.

##### Scenario: Erinnerung fällig
- **GIVEN** Erinnerung 08:00 und noch keine Messung heute
- **WHEN** 08:00 Ortszeit erreicht ist
- **THEN** wird eine Erinnerungs-Benachrichtigung erzeugt

##### Scenario: Bereits gemessen
- **GIVEN** Erinnerung 08:00 und eine Messung um 07:55
- **WHEN** 08:00 erreicht ist
- **THEN** wird keine Benachrichtigung erzeugt

##### Scenario: Morgenmessung unterdrückt die Abenderinnerung nicht
- **GIVEN** Erinnerung 20:00 und eine Messung um 08:10
- **WHEN** 20:00 erreicht ist
- **THEN** wird eine Benachrichtigung erzeugt

#### Requirement: Benachrichtigungen abrufen
Das System SHALL `GET /api/notifications` (nur eigene, ungelesene zuerst) und `POST /api/notifications/{id}/read` bereitstellen.

##### Scenario: Als gelesen markieren
- **GIVEN** eine ungelesene Benachrichtigung
- **WHEN** `POST …/read` aufgerufen wird
- **THEN** gilt sie bei der nächsten Abfrage als gelesen

### MODIFIED Requirements
_Entfällt._

### REMOVED Requirements
_Entfällt._

## 4. Design
- Kritische Werte: Hinweis und Messung in einer Transaktion (kein Verlust, kein Outbox-Betrieb nötig); die Benachrichtigungen werden in der Datenbank gehalten, Push kommt später als weiterer Kanal.
- Erinnerungen: `BackgroundService` mit `IServiceScopeFactory` (keine Captive Dependency), Zeitzonen mit `TimeZoneInfo`; Entscheidung in `docs/adr/0003-reminders-and-notifications.md`.
- Datenschutz: Benachrichtigungstext enthält Kategorie, keine Rohwerte (Push-Vorschau auf Sperrbildschirm).

## 5. Tasks
- [x] T6.1 (architect) ADR: Scheduler, transaktionale Warnung, Zeitzonen (ADR 0003)
- [x] T6.2 (backend) Tests (Red), Domain Event, Handler, Migration
- [x] T6.3 (backend) Reminder-Endpunkte und Scheduler
- [x] T6.4 (backend) Benachrichtigungs-Endpunkte, Refit
- [ ] T6.5 (frontend, Folge-Spec) Anzeige und Push-Registrierung ([Spec](../frontend-push-registrierung.openspec.md))

## 6. Acceptance Criteria (DoD)
- [x] Alle Scenarios als Tests grün (Zeit über `TimeProvider` kontrollierbar).
- [x] Kein Nachrichtenverlust bei Neustart: Messung und Hinweis teilen sich eine Transaktion; Erinnerungen werden über `LastFiredOn` höchstens einmal verarbeitet (Prozessortests). Ein verteilter Mehrinstanzbetrieb ist nicht abgedeckt (siehe ADR).
- [ ] Texte i18n-fähig (DE/EN), Haftungshinweis vorhanden.
