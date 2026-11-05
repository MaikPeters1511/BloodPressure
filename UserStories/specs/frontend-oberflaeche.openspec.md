# Capability: Blazor-Oberfläche (Auswertung, Export, Erinnerungen, Hinweise)

> Dauerhafte Capability-Spec. Quelle: archivierte Änderung `UserStories/archive/2026-11-05-frontend-blazor-oberflaeche.openspec.md`.
> Ergänzend gilt `UserStories/frontend-a11y-mobil-abnahme.openspec.md` (Mobilbreite, automatisierte A11y-Prüfung; Screenreader-Abnahme offen).

## Requirements

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

## Umgesetzt in
- Seiten: `Pages/Statistics.razor`, `Export.razor`, `Reminders.razor`, `Notifications.razor`; Komponenten `LineChart`, `CategoryBadge`; Services `IBrowserInterop`, `NotificationCounter`, `DateRangeParser`, `ChartGeometry`.
- Tests: `Tests/CleanArchitecture.UI.Tests/OverviewPagesTests.cs`, `MeasurementsPageTests.cs`.
