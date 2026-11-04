# Epic 10: Geräteimport (Apple Health, Android Health Connect, Samsung Health)

> **Status:** proposed · **Priorität:** 5 · **Abhängigkeiten:** Epic 1 (Benutzerbezug), Epic 3 (Validierung) · **Agenten:** backend-agent, frontend-agent (MAUI), architect-agent, sec-agent, ux-agent
> Ablage: `UserStories/epics/epic-10-geraeteimport.openspec.md`

## 1. Proposal (Why & What)
- **Why:** Viele Nutzer messen mit Geräten oder Uhren, deren Werte bereits in Apple Health bzw. auf Android in Samsung Health/Health Connect liegen. Manuelles Abtippen ist fehleranfällig und hält Nutzer vom Dauergebrauch ab.
- **What Changes:**
  - Die MAUI-App liest Blutdruckwerte auf dem Gerät aus **Apple Health (HealthKit)** und auf Android aus **Health Connect**; **Samsung Health** wird auf Android über Health Connect angebunden (Samsung Health schreibt dorthin). Eine direkte Anbindung über das **Samsung Health Data SDK** ist nur dann vorgesehen, wenn ein Spike zeigt, dass Pflichtdaten nicht über Health Connect ankommen.
  - Das Backend erhält einen idempotenten Batch-Import-Endpunkt und speichert Herkunft (`Source`) und externe Kennung (`ExternalId`) zur Duplikaterkennung.
  - **BREAKING (Datenmodell):** `Pulse` und `Oxygen` werden optional (`null`), weil importierte Blutdruckwerte oft ohne Puls und Sauerstoffsättigung vorliegen.
- **Impact:** Domain (`Source`, `ExternalId`, optionale Werte), Infrastructure (Migration, eindeutiger Index), Application (Import-Command), API, Contracts/Refit, MAUI (Plattform-Abstraktion, Berechtigungen, UI). Das Backend spricht **nie** direkt mit Health-Plattformen; Gerätedaten werden ausschließlich auf dem Gerät gelesen und von der App hochgeladen.

## 2. User Stories
- Als iPhone-Nutzer möchte ich meine Blutdruckwerte aus Apple Health übernehmen, damit ich sie nicht abtippen muss.
- Als Android-Nutzer mit Samsung-Gerät oder Galaxy Watch möchte ich meine Werte aus Samsung Health übernehmen, damit alles an einem Ort liegt.
- Als Nutzer möchte ich bei Berechtigungen selbst entscheiden, damit nur Blutdruckdaten gelesen werden.
- Als Nutzer möchte ich, dass wiederholtes Importieren keine Doppelten erzeugt.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Idempotenter Batch-Import
Das System SHALL `POST /api/BloodPressure/import` mit bis zu 500 Einträgen `{ source, externalId, systolisch, diastolisch, dateTime, pulse?, oxygen? }` annehmen und je Eintrag anlegen, überspringen (bereits vorhanden) oder ablehnen (ungültig). Die Antwort SHALL `{ imported, skipped, rejected: [{ index, errors }] }` enthalten. Die Kombination `(UserId, Source, ExternalId)` SHALL eindeutig sein.

##### Scenario: Erster Import
- **GIVEN** 3 gültige Einträge mit `externalId`
- **WHEN** sie importiert werden
- **THEN** ist `imported = 3`, `skipped = 0`

##### Scenario: Wiederholter Import
- **GIVEN** dieselben 3 Einträge sind bereits importiert
- **WHEN** der Import erneut gesendet wird
- **THEN** ist `imported = 0`, `skipped = 3` und die Datenbank enthält weiterhin 3 Messungen

##### Scenario: Teilweise ungültig
- **GIVEN** 2 gültige Einträge und einer mit Systolisch 999
- **WHEN** importiert wird
- **THEN** ist `imported = 2` und `rejected` enthält den Index des dritten mit Feldfehler

##### Scenario: Zu viele Einträge
- **GIVEN** 501 Einträge
- **WHEN** der Import gesendet wird
- **THEN** antwortet die API mit 400, ohne etwas zu speichern

#### Requirement: Herkunft der Messung
Das System SHALL die Herkunft als `Manual`, `AppleHealth`, `HealthConnect` oder `SamsungHealth` speichern und im DTO ausgeben. Manuell angelegte Messungen haben `Source = Manual` und keine `ExternalId`.

##### Scenario: Manuelle Messung
- **GIVEN** eine über `POST /api/BloodPressure` angelegte Messung
- **WHEN** sie gelesen wird
- **THEN** ist `source = Manual` und `externalId = null`

#### Requirement: Gerätedaten lesen (Plattform-Abstraktion)
Die MAUI-App SHALL Gerätedaten über ein Interface `IHealthDataSource` lesen (`RequestPermissionAsync`, `ReadBloodPressureAsync(since)`), mit Implementierungen für iOS (HealthKit-Korrelation Blutdruck) und Android (Health Connect `BloodPressureRecord`). Auf Plattformen ohne Health-Dienst SHALL die Funktion ausgeblendet werden.

##### Scenario: Berechtigung verweigert
- **GIVEN** der Nutzer verweigert den Lesezugriff
- **WHEN** er „Importieren“ wählt
- **THEN** werden keine Daten gelesen und die App erklärt verständlich, wie er die Berechtigung erteilt

##### Scenario: Nur Blutdruck
- **GIVEN** der Nutzer erteilt Zugriff
- **WHEN** die App Berechtigungen anfragt
- **THEN** werden nur Blutdruck (und optional Puls und Sauerstoffsättigung) angefragt, kein weiterer Datentyp

#### Requirement: Samsung Health als Quelle erkennen
Die Android-Implementierung SHALL Datensätze, deren Datenursprung (`dataOrigin.packageName`) Samsung Health ist (erwartet `com.sec.android.app.shealth`, im Spike zu verifizieren), mit `Source = SamsungHealth` kennzeichnen, alle anderen Health-Connect-Datensätze mit `Source = HealthConnect`.

##### Scenario: Wert aus Samsung Health
- **GIVEN** ein Health-Connect-Datensatz mit Samsung Health als Ursprung
- **WHEN** er importiert wird
- **THEN** speichert das Backend `Source = SamsungHealth`

##### Scenario: Wert aus anderer App
- **GIVEN** ein Datensatz einer anderen Gesundheits-App
- **WHEN** er importiert wird
- **THEN** speichert das Backend `Source = HealthConnect`

#### Requirement: Puls und Sauerstoff zuordnen
Die App SHALL zu einer Blutdruckmessung den zeitlich nächsten Pulswert und Sauerstoffsättigungswert innerhalb von ±5 Minuten übernehmen (Health Connect und HealthKit führen sie als getrennte Datensätze); gibt es keinen, bleiben die Felder `null`.

##### Scenario: Puls in der Nähe
- **GIVEN** Blutdruck um 08:00 und Puls um 08:02
- **WHEN** importiert wird
- **THEN** trägt die Messung den Puls von 08:02

##### Scenario: Puls zu weit entfernt
- **GIVEN** Blutdruck um 08:00 und Puls um 08:20
- **WHEN** importiert wird
- **THEN** ist `pulse = null`

#### Requirement: Inkrementeller Import
Die App SHALL den Zeitpunkt des letzten erfolgreichen Imports je Quelle lokal speichern und nur neuere Datensätze lesen; ein manueller Vollimport („Alles erneut prüfen“) SHALL möglich sein.

##### Scenario: Zweiter Import
- **GIVEN** ein erfolgreicher Import vor einer Stunde
- **WHEN** der Nutzer erneut importiert
- **THEN** werden nur Datensätze seit diesem Zeitpunkt gelesen

#### Requirement: Datenschutz und Transparenz
Das System SHALL Gerätedaten nur zum Konto des Nutzers speichern, nicht an Dritte weitergeben und dem Nutzer vor der ersten Berechtigung Zweck und Umfang erklären. Der Nutzer SHALL importierte Daten einer Quelle gesammelt löschen können.

##### Scenario: Quelle entfernen
- **GIVEN** importierte Messungen aus `SamsungHealth`
- **WHEN** der Nutzer „Daten dieser Quelle löschen“ wählt (`DELETE /api/BloodPressure?source=SamsungHealth`)
- **THEN** sind genau diese Messungen gelöscht, manuelle und andere bleiben

### MODIFIED Requirements
**Messungsdaten:** `Pulse` und `Oxygen` SHALL optional sein (`int?`), mit Wertebereich 0–500 bzw. 0–100, sofern gesetzt. `BloodPressureDto`, `BloodPressureRequest`, Datenbankspalten (nullable, neue Migration) und `Api.razor`/Statistik (Epic 4: Durchschnitte ignorieren `null`) SHALL entsprechend angepasst werden. Bestehende Werte bleiben unverändert.

### REMOVED Requirements
_Entfällt._

## 4. UI/UX Requirements
- **Komponenten:** MAUI-Seite „Import“ mit Quellenwahl (nur verfügbare Quellen), Berechtigungserklärung, Fortschritt und Ergebnisansicht (importiert/übersprungen/abgelehnt); ViewModel `ImportViewModel` (MVVM, Microsoft DI), Dienst `IHealthDataSource` je Plattform.
- **State:** letzter Importzeitpunkt je Quelle (lokal), laufender Import, Ergebnis.
- **i18n/a11y:** alle Texte DE/EN in `.resx`; Screenreader-Beschriftungen für Fortschritt und Ergebnis; Berechtigungshinweise in einfacher Sprache.

## 5. API & Backend Requirements
- **Authentifizierung:** Import nur für angemeldete Nutzer (Epic 1), `UserId` aus Token, nie aus dem Request.
- **Endpoints:** `POST /api/BloodPressure/import`, `DELETE /api/BloodPressure?source=…`.
- **Modelle:** `BloodPressure` + `Source` (Enum, gespeichert als Text), `ExternalId` (string, max. 128, nullable), `Pulse?`, `Oxygen?`; eindeutiger Index `(UserId, Source, ExternalId)` (gefiltert auf `ExternalId IS NOT NULL`).

## 6. Design (Technischer Ansatz)
- **Wo wird gelesen:** ausschließlich auf dem Gerät; das Backend bleibt plattformunabhängig und testbar.
- **Samsung Health:** Primärer Weg ist Health Connect (Datenfluss Samsung Health → Health Connect, Nutzer aktiviert die Synchronisierung in Samsung Health). Direktweg über das Samsung Health Data SDK nur nach Spike (T10.1), denn er bringt zusätzliche Anforderungen (Entwicklermodus/Freigabe durch Samsung, nur Samsung-Geräte). Entscheidung per ADR.
- **.NET MAUI:** HealthKit steht über die iOS-Bindings bereit. Für Health Connect gibt es keine offizielle MAUI-Bindung; geplant ist eine schlanke Android-Binding-Bibliothek (Kotlin/Java-Wrapper um `androidx.health.connect:connect-client`) – Machbarkeit im Spike prüfen.
- **Duplikate:** `ExternalId` = HealthKit-UUID bzw. Health-Connect-`metadata.id`; Fallback bei fehlender ID: Hash aus Quelle + Zeitpunkt + Werte.
- **Zeit:** Messzeitpunkt immer UTC; Zeitzonenversatz der Geräte beachten.
- **Stores/Compliance:** iOS braucht HealthKit-Entitlement und `NSHealthShareUsageDescription`; Google Play verlangt für Health-Connect-Berechtigungen eine Datenschutzerklärung und das Ausfüllen der Health-Apps-Deklaration; Samsung SDK ggf. eigene Bedingungen. Frühzeitig prüfen, da Freigaben Wochen dauern können.
- **Risiken:** Health-Connect-Verfügbarkeit (ab Android 14 im System, davor eigene App), nicht alle Samsung-Modelle messen Blutdruck (Uhren teils nur in bestimmten Regionen/mit Kalibrierung), Hintergrundsynchronisierung ist plattformspezifisch und bleibt zunächst aus (nur manuell auslösbar).
- **Alternativen:** Server-seitige Cloud-Anbindung (Samsung/Apple bieten keine einfache Server-API für Nutzerdaten) – verworfen; CSV-Import als Fallback für andere Geräte (Folge-Idee).

## 7. Tasks
- [ ] T10.1 (architect/frontend) **Spike:** Health Connect aus MAUI (Binding-Aufwand), Samsung-Health-Daten via Health Connect vollständig? Package-Name und Felder prüfen; Samsung Health Data SDK nur bei Lücke bewerten → ADR
- [ ] T10.2 (backend) Tests für Import-Scenarios (Red): idempotent, Teilfehler, Limit, Herkunft
- [ ] T10.3 (backend) Domain/Migration: `Source`, `ExternalId`, optionale Werte, eindeutiger Index; Statistik/DTO anpassen
- [ ] T10.4 (backend) Import-Endpunkt, Quelle-löschen-Endpunkt, Refit-Methoden
- [ ] T10.5 (frontend) `IHealthDataSource` + Zuordnungslogik Puls/Sauerstoff als reine, testbare Klasse (Tests zuerst)
- [ ] T10.6 (frontend) iOS-Implementierung (HealthKit, Entitlement, Info.plist)
- [ ] T10.7 (frontend) Android-Implementierung (Health Connect, Samsung-Erkennung, Manifest-Berechtigungen)
- [ ] T10.8 (frontend/ux) Import-Seite, Berechtigungstexte, i18n DE/EN, ARIA
- [ ] T10.9 (sec) Datenschutzprüfung, Berechtigungsumfang, Store-Deklarationen vorbereiten
- [ ] T10.10 (qa) Gerätetests auf echtem iPhone und Samsung-Gerät (Emulator liefert keine echten Daten), Testplan
- [ ] T10.11 (doc) Anleitung „Samsung Health mit Health Connect verbinden“

## 8. Acceptance Criteria (DoD)
- [ ] Alle Scenarios des Backends als Tests grün; Zuordnungslogik der App als Unit-Tests grün.
- [ ] Import auf einem echten iPhone und einem Samsung-Gerät mit realen Werten erfolgreich, zweiter Lauf erzeugt keine Duplikate.
- [ ] Es werden nur Blutdruck, Puls und Sauerstoffsättigung angefragt (geprüft in Manifest/Info.plist).
- [ ] Datenschutztexte und Store-Deklarationen liegen vor.
- [ ] Spec archiviert, Requirements in `UserStories/specs/` übernommen.
