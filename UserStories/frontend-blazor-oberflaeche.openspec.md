# Change: Blazor-Oberfläche für Statistik, Export, Erinnerungen und Hinweise

> **Status:** in-progress
> Ablage: `UserStories/frontend-blazor-oberflaeche.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-frontend-blazor-oberflaeche.openspec.md`)
> Baut auf: `frontend-blazor-api-anpassung.openspec.md` (Login, Messwerte), Epics 4, 5, 6.

## 1. Proposal (Why & What)
- **Why:** Die API bietet Statistik, Klassifikation, Export, Erinnerungen und Hinweise, die Oberfläche nutzt bisher nur Login und Messwerte. Nutzer sehen weder Verläufe noch Warnungen und können Erinnerungen nicht einstellen.
- **What Changes:** Vier neue Seiten bzw. Bereiche im Blazor-WASM-Client: Auswertung (Statistik + Verlauf), Export (CSV/PDF), Erinnerungen, Hinweise (mit Ungelesen-Zähler in der Navigation). Dazu die Kategorie der Messung farblich **und** textlich in der Messwertliste.
- **Impact:** `CleanArchitecture.Presentation.UI` (Pages, Services, Ressourcen), `Tests/CleanArchitecture.UI.Tests`. **Kein** API-Change, **keine** neue Abhängigkeit. **Nicht** Teil: MAUI, KI (Epic 9), Geräteimport (Epic 10).

## 2. User Stories
- Als Nutzer möchte ich meine Werte je Tag, Woche oder Monat als Kennzahlen und Verlauf sehen, damit ich Trends erkenne.
- Als Nutzer möchte ich meine Messungen eines Zeitraums als CSV oder PDF herunterladen, damit ich sie meinem Arzt geben kann.
- Als Nutzer möchte ich tägliche Erinnerungen einstellen, damit ich das Messen nicht vergesse.
- Als Nutzer möchte ich Warnhinweise zu kritischen Werten lesen und als gelesen markieren.
- Als Nutzer möchte ich in der Liste sofort erkennen, in welche Kategorie ein Wert fällt.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Auswertung
Das System SHALL unter `/statistics` die Kennzahlen (Anzahl, Durchschnitt, Min, Max für Systolisch, Diastolisch, Puls) aus `GetStatisticsAsync` je Intervall `day`/`week`/`month` und wählbarem Zeitraum (Standard: letzte 30 Tage) anzeigen, zusätzlich als Verlaufsdiagramm der Durchschnittswerte.

##### Scenario: Werte vorhanden
- **GIVEN** ein angemeldeter Nutzer mit Messungen im Zeitraum
- **WHEN** er die Seite öffnet
- **THEN** zeigt eine Tabelle je Zeitabschnitt die Kennzahlen und das Diagramm die Durchschnittsverläufe

##### Scenario: Intervall wechseln
- **GIVEN** die Auswertung ist sichtbar
- **WHEN** der Nutzer „Woche“ wählt
- **THEN** wird die API mit `interval=week` und dem gewählten Zeitraum aufgerufen und die Anzeige aktualisiert

##### Scenario: Keine Daten
- **GIVEN** keine Messung im Zeitraum
- **WHEN** die Seite lädt
- **THEN** erscheint ein erklärender Leerzustand statt einer leeren Tabelle

##### Scenario: Ungültiger Zeitraum
- **GIVEN** „Von“ liegt nach „Bis“
- **WHEN** der Nutzer den Zeitraum anwendet
- **THEN** erscheint eine Validierungsmeldung und es wird keine Anfrage gesendet

#### Requirement: Diagramm ohne Zusatzabhängigkeit und barrierefrei
Das System SHALL das Diagramm als inline-SVG selbst rendern (keine Chart-Bibliothek, keine neue Lizenz) und zusätzlich die Werte als Tabelle bereitstellen, die für Screenreader die maßgebliche Darstellung ist.

##### Scenario: Screenreader
- **GIVEN** ein Diagramm
- **WHEN** es gerendert wird
- **THEN** besitzt es `role="img"` mit lokalisiertem `aria-label`, und die Unterscheidung der Reihen erfolgt nicht allein über Farbe (Beschriftung/Legende, unterschiedliche Linienform)

#### Requirement: Kategorie in der Messwertliste
Das System SHALL in der Messwertliste die Kategorie jeder Messung als lokalisierten Text mit Farbmarkierung anzeigen.

##### Scenario: Erhöhter Wert
- **GIVEN** eine Messung mit Kategorie `Grade2`
- **WHEN** die Liste angezeigt wird
- **THEN** steht der lokalisierte Kategorietext neben dem Wert, die Information ist auch ohne Farbwahrnehmung verfügbar

#### Requirement: Export
Das System SHALL unter `/export` den Download von CSV oder PDF für einen optionalen Zeitraum anbieten und die Datei per Browser speichern lassen.

##### Scenario: CSV laden
- **GIVEN** ein gewählter Zeitraum und Format CSV
- **WHEN** der Nutzer „Herunterladen“ wählt
- **THEN** ruft der Client `ExportAsync("csv", from, to)` auf und löst den Dateidownload mit sprechendem Dateinamen (`blutdruck-<von>-<bis>.csv`) aus

##### Scenario: Zu viele Messungen
- **GIVEN** die API antwortet mit 400 wegen mehr als 10 000 Messungen
- **WHEN** der Download angefordert wird
- **THEN** zeigt die Seite die Meldung der API sinngemäß lokalisiert an und bietet an, den Zeitraum zu verkleinern

##### Scenario: Während des Ladens
- **GIVEN** ein laufender Download
- **WHEN** der Nutzer erneut klickt
- **THEN** ist die Schaltfläche deaktiviert und ein Ladezustand wird per `aria-busy`/Statustext angekündigt

#### Requirement: Erinnerungen verwalten
Das System SHALL unter `/reminders` die gespeicherten Erinnerungen (Zeitzone, bis zu 5 Uhrzeiten) laden, bearbeiten und speichern.

##### Scenario: Speichern
- **GIVEN** der Nutzer wählt eine Zeitzone und drei Uhrzeiten
- **WHEN** er speichert
- **THEN** sendet der Client diese Daten, bestätigt den Erfolg und zeigt den gespeicherten Stand

##### Scenario: Clientseitige Grenzen
- **GIVEN** fünf Uhrzeiten sind eingetragen
- **WHEN** der Nutzer eine sechste hinzufügen will
- **THEN** ist „Hinzufügen“ deaktiviert; doppelte oder ungültige Zeiten erzeugen eine Feldmeldung vor dem Senden

##### Scenario: Serverfehler pro Feld
- **GIVEN** die API antwortet mit `ValidationProblemDetails` (z. B. unbekannte Zeitzone)
- **WHEN** gespeichert wird
- **THEN** erscheint die Meldung am betroffenen Feld und der Fokus springt darauf

#### Requirement: Hinweise
Das System SHALL unter `/notifications` die Hinweise (neueste zuerst) anzeigen, einzeln als gelesen markieren lassen und in der Navigation die Anzahl ungelesener Hinweise ausgeben.

##### Scenario: Ungelesen-Zähler
- **GIVEN** zwei ungelesene Hinweise
- **WHEN** der Nutzer angemeldet ist
- **THEN** zeigt der Navigationseintrag „Hinweise“ die Zahl 2, auch per `aria-label` ansagbar („2 ungelesene Hinweise“)

##### Scenario: Als gelesen markieren
- **GIVEN** ein ungelesener Hinweis
- **WHEN** der Nutzer „Als gelesen markieren“ wählt
- **THEN** wird `POST …/read` aufgerufen, der Hinweis erscheint gelesen und der Zähler sinkt

##### Scenario: Kritischer Wert
- **GIVEN** ein Hinweis vom Typ `CriticalReading`
- **WHEN** er angezeigt wird
- **THEN** ist er als Warnung gekennzeichnet (Text/Icon + `role="alert"` nur beim erstmaligen Erscheinen), ohne Messwerte im Text und mit Hinweis, dass dies keine ärztliche Beratung ersetzt

#### Requirement: Navigation und Zugriffsschutz
Das System SHALL die neuen Seiten nur angemeldeten Nutzern anzeigen; bei fehlender oder abgelaufener Anmeldung (401) leitet der Client wie bei den Messwerten auf `/login` um.

##### Scenario: Nicht angemeldet
- **GIVEN** kein Token
- **WHEN** `/statistics`, `/export`, `/reminders` oder `/notifications` geöffnet wird
- **THEN** erfolgt die Weiterleitung zu `/login`

#### Requirement: i18n und Barrierefreiheit
Das System SHALL alle neuen Texte über `IStringLocalizer` (`SharedResource` DE/EN) ausgeben, Datums-/Zahlenformate nach Kultur darstellen, alle Eingaben mit `<label>` verknüpfen und vollständig per Tastatur bedienbar machen.

##### Scenario: Englische Kultur
- **GIVEN** Kultur `en`
- **WHEN** eine der neuen Seiten gerendert wird
- **THEN** sind alle Texte englisch und es fehlen keine Schlüssel (Test prüft DE/EN-Schlüsselparität)

### MODIFIED Requirements
_Entfällt._

### REMOVED Requirements
_Entfällt._ (Die Vorlagenseiten `Counter` und `Weather` sind bereits unverlinkt; Löschen wäre eigener, kleiner Change.)

## 4. UI/UX Requirements
- **Komponenten:** `Pages/Statistics.razor`, `Pages/Export.razor`, `Pages/Reminders.razor`, `Pages/Notifications.razor`; wiederverwendbar: `Components/LineChart.razor` (SVG), `Components/DateRangePicker.razor`, `Components/CategoryBadge.razor`; `NavMenu` um drei Einträge und Zähler erweitert.
- **Services:** `FileDownloader` (JS-Interop, `Blob` → Download), `NotificationCounter` (hält ungelesene Anzahl, löst `Changed` aus; gleicher Stil wie `AuthSession`); gemeinsame Fehlerauswertung über das vorhandene `ApiErrors`.
- **State:** seitenlokal (Zeitraum, Intervall, Formularstand); nur der Ungelesen-Zähler ist global. Kein Zustandscontainer-Framework.
- **i18n / a11y:** neue Schlüssel in `SharedResource.resx` und `.en.resx` (Präfixe `Stats_`, `Export_`, `Reminders_`, `Notifications_`, `Category_`). Fehler mit `role="alert"`, Erfolg mit `role="status"`. Farben mit Kontrast ≥ 4,5:1, Information nie nur über Farbe.
- **Styling:** Bootstrap-Klassen wie in den vorhandenen Seiten; Syncfusion nur, wo es schon genutzt wird (Grid der Messwerte).

## 5. API & Backend Requirements
- **Authentifizierung:** Bearer wie bestehend; kein neuer Endpunkt, keine Rollen.
- **Genutzte Endpunkte:** `GET /api/v1/BloodPressure/statistics`, `GET …/export`, `GET/PUT /api/v1/reminders`, `GET /api/v1/notifications`, `POST /api/v1/notifications/{id}/read` (alle über die Refit-Clients aus `Contracts`).
- **Prüfen vor Umsetzung:** ob `IReminderApi` ein Speichern (PUT) bietet und der Client im UI registriert ist (`AddBloodPressureApiClient` deckt bisher ggf. nur `IBloodPressureApi`/`IAuthApi` ab); falls Lücke: Registrierung im UI ergänzen, kein API-Change.
- **CORS:** `Content-Disposition` wird nicht gelesen (Dateiname kommt vom Client), daher keine Exposed-Headers-Änderung nötig.

## 6. KI/AI Requirements
Entfällt.

## 7. Design (Technischer Ansatz)
- **Diagramm selbst gebaut statt Bibliothek:** drei Linien (Sys/Dia/Puls) reichen; vermeidet Syncfusion-Chart-Lizenz und Bundle-Größe. Risiko: eigener Code, deshalb Tests über Berechnung der Koordinaten (rein, ohne DOM) und bUnit für Markup.
- **Download:** `Stream` → `DotNetStreamReference` → JS `URL.createObjectURL`; `Stream` wird nach dem Lesen freigegeben. Bis 10 000 Messungen liegen im Speicher des Browsers, akzeptiert.
- **Zeitzone:** Auswahl aus `TimeZoneInfo.GetSystemTimeZones()` im WASM ist plattformabhängig; Vorgabe: Vorschlag aus dem Browser (`Intl.DateTimeFormat().resolvedOptions().timeZone`), Freitext mit Prüfung durch die API. Entscheidung bei Umsetzung am echten Browser verifizieren.
- **Zeiträume:** Datumsauswahl in lokaler Zeit, an die API als UTC-Grenzen (Beginn des Starttags bis Ende des Endtags).
- **Ungelesen-Zähler:** Abruf beim Anmelden und nach „gelesen“, kein Polling in dieser Stufe (Folgeoption: Intervall).
- **Risiko:** Hinweise berühren Gesundheitsdaten; Texte bleiben ohne Messwerte (so liefert sie die API), und ein Haftungsausschluss („keine ärztliche Beratung“) steht auf Auswertung und Hinweisen.

## 8. Tasks (Implementierungs-Checkliste)
- [x] T1 (frontend) Tests (Red) aus den Scenarios: bUnit je Seite, Koordinatenberechnung `LineChart`, DE/EN-Schlüsselparität, Redirect ohne Login
- [x] T2 (frontend) Refit-Clients im UI registrieren/prüfen, `FileDownloader` + JS, `NotificationCounter`
- [x] T3 (frontend) `CategoryBadge` und Messwertliste
- [x] T4 (frontend) Auswertung mit `LineChart` und `DateRangePicker`
- [x] T5 (frontend) Export
- [x] T6 (frontend) Erinnerungen
- [x] T7 (frontend) Hinweise und Navigation mit Zähler
- [x] T8 (ux) Review: Tastatur, Fokusführung, Kontraste, Accessibility-Baum geprüft (Befunde behoben: Fokusverlust nach „gelesen“/„Entfernen“, Singular-Label, fehlende `visually-hidden`-Klasse). Mobilbreite und echter Screenreader (VoiceOver/NVDA) nicht geprüft
- [x] T9 (qa) Durchlauf im echten Browser gegen die echte API (Registrieren → Messungen → Auswertung → Export → Erinnerung → Hinweis); Ergebnis dokumentieren
- [x] T10 (doc) README: Funktionsübersicht der Oberfläche

## 9. Acceptance Criteria (DoD)
- [x] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün (lokal: 205 Tests, UI-Tests auch mit englischer Systemkultur; CI steht aus).
- [x] Keine hartcodierten sichtbaren Texte; DE/EN-Paritätstest grün.
- [x] Keine neue NuGet-Abhängigkeit; kein Lizenzschlüssel im Repository.
- [x] Im Browser geprüft: sichtbarer Fokusindikator an allen 14 bedienbaren Elementen der Auswertung, Fokusziele nach Aktionen, Accessibility-Baum (Labels, Formulare, Diagramm `role=img` + Tabelle). **Nicht** geprüft: echter Screenreader, Mobilbreite.
- [x] Download von CSV und PDF im echten Browser geprüft: CSV-Inhalt (Semikolon, 5 Zeilen, Kategorien) und PDF (`%PDF-1.4` … `%%EOF`, 34 KB) stimmen; Dateinamen korrekt.
- [ ] Spec archiviert, Requirements in `UserStories/specs/frontend-oberflaeche.openspec.md` übernommen.
