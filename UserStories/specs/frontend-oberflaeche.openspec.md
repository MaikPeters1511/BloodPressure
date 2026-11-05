# Capability: Blazor-Oberfläche (Auswertung, Export, Erinnerungen, Hinweise)

> Dauerhafte Capability-Spec. Quelle: archivierte Änderung `UserStories/archive/2026-11-05-frontend-blazor-oberflaeche.openspec.md`.
> Ergänzend gilt `UserStories/archive/2026-11-05-frontend-a11y-mobil-abnahme.openspec.md` (Mobilbreite, automatisierte A11y-Prüfung; Screenreader-Abnahme offen).

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

## Aus der archivierten Änderung `frontend-blazor-api-anpassung`

#### Requirement: Anmeldung und Registrierung
Das System SHALL eine Seite `/login` mit E-Mail und Passwort bereitstellen, über die sich Nutzer anmelden oder registrieren (Registrierung meldet danach direkt an). Nach Erfolg SHALL zur Messwertliste navigiert werden. Fehler SHALL als lesbarer Text mit `role="alert"` erscheinen (ungültige Zugangsdaten; bei Registrierung die Fehler der API je Feld), niemals als Stacktrace.

##### Scenario: Erfolgreicher Login
- **GIVEN** gültige Zugangsdaten
- **WHEN** der Nutzer „Anmelden“ wählt
- **THEN** werden die Tokens gespeichert und die Messwertliste geöffnet

##### Scenario: Falsches Passwort
- **GIVEN** ungültige Zugangsdaten (API antwortet 401)
- **WHEN** der Nutzer „Anmelden“ wählt
- **THEN** erscheint die lokalisierte Meldung „E-Mail oder Passwort ist falsch“ und es wird nicht navigiert

##### Scenario: Registrierung mit zu schwachem Passwort
- **GIVEN** die API lehnt das Passwort mit 400 und Feldfehlern ab
- **WHEN** der Nutzer „Registrieren“ wählt
- **THEN** werden die Fehlermeldungen der API angezeigt

#### Requirement: Tokenverwaltung
Das System SHALL das Access-Token bei jedem API-Aufruf mitsenden, ein abgelaufenes Token mit dem Refresh-Token erneuern und beim Abmelden bzw. bei fehlgeschlagener Erneuerung alle Tokens löschen. Die Tokens SHALL nur in `sessionStorage` liegen (verfällt mit dem Tab).

##### Scenario: Abgelaufenes Token
- **GIVEN** ein abgelaufenes Access-Token und ein gültiges Refresh-Token
- **WHEN** das Token angefordert wird
- **THEN** wird ein neues Token-Paar geholt und gespeichert

##### Scenario: Erneuerung scheitert
- **GIVEN** ein abgelaufenes Access-Token und ein ungültiges Refresh-Token
- **WHEN** das Token angefordert wird
- **THEN** werden die Tokens gelöscht und der Nutzer gilt als abgemeldet

##### Scenario: Wiederherstellen nach Reload
- **GIVEN** gespeicherte Tokens in `sessionStorage`
- **WHEN** die App neu geladen wird
- **THEN** gilt der Nutzer weiterhin als angemeldet

#### Requirement: Messwertliste
Das System SHALL unter `/measurements` (nur für Angemeldete; sonst Weiterleitung auf `/login`) die Messwerte seitenweise (10 je Seite, neueste zuerst) in einer Tabelle mit Datum, Systolisch, Diastolisch, Puls, Sauerstoff und lokalisierter Kategorie anzeigen, mit Navigation „Vorherige/Nächste Seite“ und Anzeige „Seite x von y“. Ohne Messwerte SHALL ein Hinweistext erscheinen. Ein Hinweis SHALL erklären, dass die Kategorie keine Diagnose ist.

##### Scenario: Liste mit zwei Seiten
- **GIVEN** 12 Messwerte
- **WHEN** die Seite geöffnet wird
- **THEN** zeigt die Tabelle 10 Zeilen, „Seite 1 von 2“, „Vorherige“ ist deaktiviert

##### Scenario: Nächste Seite
- **GIVEN** die erste von zwei Seiten
- **WHEN** „Nächste“ gewählt wird
- **THEN** wird Seite 2 abgerufen und mit 2 Zeilen angezeigt

##### Scenario: Nicht angemeldet
- **GIVEN** kein Nutzer ist angemeldet
- **WHEN** `/measurements` geöffnet wird
- **THEN** wird nach `/login` weitergeleitet

##### Scenario: Token ungültig
- **GIVEN** die API antwortet mit 401
- **WHEN** die Liste geladen wird
- **THEN** wird abgemeldet und auf `/login` weitergeleitet

#### Requirement: Messwert anlegen
Das System SHALL ein Formular mit beschrifteten Feldern (Systolisch, Diastolisch, Puls, Sauerstoffsättigung, Zeitpunkt) anbieten, den Messwert über die API anlegen und danach die Liste neu laden. Feldfehler der API (400) SHALL beim jeweiligen Feld bzw. gesammelt mit `role="alert"` erscheinen.

##### Scenario: Gültiger Messwert
- **GIVEN** gültige Eingaben
- **WHEN** der Nutzer „Speichern“ wählt
- **THEN** wird `CreateAsync` aufgerufen und die Liste enthält den neuen Eintrag

##### Scenario: Ungültiger Messwert
- **GIVEN** die API antwortet mit 400 und Fehlern zu `systolisch`
- **WHEN** der Nutzer „Speichern“ wählt
- **THEN** wird die Fehlermeldung angezeigt und die Eingaben bleiben erhalten

#### Requirement: Messwert löschen
Das System SHALL je Zeile eine Schaltfläche „Löschen“ (mit zugänglichem Namen inkl. Datum) bieten, die nach Bestätigung den Messwert mit `If-Match` löscht und die Liste neu lädt. Bei 412 SHALL eine Meldung zum veralteten Stand erscheinen und die Liste neu geladen werden.

##### Scenario: Löschen
- **GIVEN** ein Messwert in der Liste
- **WHEN** der Nutzer „Löschen“ wählt und bestätigt
- **THEN** wird `DeleteAsync(id, ETag)` aufgerufen und die Liste neu geladen

##### Scenario: Veralteter Stand
- **GIVEN** die API antwortet mit 412
- **WHEN** der Nutzer löscht
- **THEN** erscheint eine Meldung, dass der Eintrag zwischenzeitlich geändert wurde, und die Liste wird neu geladen

#### Requirement: Mehrsprachigkeit und Barrierefreiheit
Das System SHALL alle sichtbaren Texte der neuen Seiten aus `.resx`-Ressourcen (DE Standard, EN) beziehen und die Sprache aus der Browsersprache wählen. Formularfelder SHALL sichtbare, per `for`/`id` verknüpfte Labels haben, Tabellen eine `caption`, dynamische Meldungen `role="alert"`/`aria-live`, alle Funktionen per Tastatur erreichbar.

##### Scenario: Englische Oberfläche
- **GIVEN** die Kultur `en`
- **WHEN** die Login-Seite gerendert wird
- **THEN** sind Überschrift und Beschriftungen englisch

##### Scenario: Labels
- **GIVEN** das Anlegen-Formular
- **WHEN** es gerendert wird
- **THEN** gehört zu jedem Eingabefeld genau ein `label` mit passendem `for`

## Aus der archivierten Änderung `frontend-a11y-mobil-abnahme`

#### Requirement: Mobile Darstellung
Das System SHALL alle Seiten (`/login`, `/measurements`, `/statistics`, `/export`, `/reminders`, `/notifications`) bei Viewport-Breiten von 320 px bis 1280 px ohne horizontales Scrollen der Seite darstellen; Tabellen dürfen innerhalb ihres Containers scrollen.

##### Scenario: Handybreite
- **GIVEN** ein Viewport von 375 × 812 px
- **WHEN** eine der Seiten geöffnet wird
- **THEN** ist `document.documentElement.scrollWidth` nicht größer als die Viewport-Breite und kein Inhalt wird abgeschnitten

##### Scenario: Kleinste Breite
- **GIVEN** ein Viewport von 320 px Breite
- **WHEN** das Formular der Messwerte und das Diagramm der Auswertung angezeigt werden
- **THEN** bleiben Beschriftungen lesbar, das Diagramm skaliert in die Breite und die Legende bricht um

##### Scenario: Navigation auf dem Handy
- **GIVEN** Viewport unter 641 px
- **WHEN** der Nutzer den Menüknopf per Tippen oder Tastatur bedient
- **THEN** klappt die Navigation auf/zu, `aria-expanded` entspricht dem Zustand und alle Einträge sind erreichbar

#### Requirement: Zielgröße
Das System SHALL interaktive Elemente (Schaltflächen, Links in der Navigation, Eingabefelder, Auswahlfelder) mit mindestens 44 × 44 CSS-Pixeln Trefferfläche bereitstellen (WCAG 2.2 Ziel AAA 2.5.5; mindestens 24 × 24 nach 2.5.8).

##### Scenario: Messung der Zielgröße
- **GIVEN** Viewport 375 px
- **WHEN** alle sichtbaren interaktiven Elemente vermessen werden
- **THEN** unterschreitet keines 24 × 24 px, und die primären Schaltflächen erreichen 44 px Höhe

#### Requirement: Automatisierter A11y-Check
Das System SHALL für jede Seite einen automatisierten axe-core-Lauf (Regelsatz WCAG 2.1/2.2 A und AA) im Browser durchführen und bei Verstößen der Schwere „serious“ oder „critical“ fehlschlagen.

##### Scenario: Keine schweren Verstöße
- **GIVEN** die Oberfläche läuft gegen die API mit Testdaten (angemeldet, mit Messungen, Hinweisen, Erinnerungen)
- **WHEN** axe-core für jede Seite in Hell- und Dunkeldarstellung ausgeführt wird
- **THEN** meldet es keinen Verstoß der Schwere „serious“ oder „critical“, übrige Funde sind in der Prüfdatei begründet

#### Requirement: Screenreader-Prüfanleitung und Protokoll
Das System SHALL eine Prüfanleitung `docs/a11y/screenreader-pruefung.md` enthalten mit je einem Ablauf pro Seite (Schritte, erwartete Ansage) für VoiceOver (macOS/Safari) und NVDA (Windows/Firefox oder Chrome), sowie eine Ergebnistabelle (Bestanden/Befund, Datum, Prüfer, Version).

##### Scenario: Durchlauf
- **GIVEN** die Anleitung und eine laufende Oberfläche mit Testdaten
- **WHEN** eine Person alle Abläufe mit Screenreader durchführt
- **THEN** ist jeder Ablauf als „bestanden“ oder mit konkretem Befund (Seite, Schritt, gehört/erwartet) protokolliert

##### Scenario: Pflichtabläufe
- **GIVEN** die Anleitung
- **WHEN** sie geprüft wird
- **THEN** enthält sie mindestens: Anmeldung mit Fehlermeldung; Messung anlegen mit Validierungsfehler; Messwertliste (Kategorie als Text); Auswertung (Diagramm-Beschreibung und Tabelle); Export mit Status „Datei heruntergeladen“; Erinnerung speichern mit Fehlerfokus; Hinweis als gelesen markieren (Fokus und Statusansage); Zähler „ungelesene Hinweise“ in der Navigation

#### Requirement: Befunde schließen
Das System SHALL jeden protokollierten Befund entweder durch eine Korrektur mit Test beheben oder als bewusste Abweichung mit Begründung dokumentieren.

##### Scenario: Befund behoben
- **GIVEN** ein Befund im Protokoll
- **WHEN** die Korrektur umgesetzt wird
- **THEN** verweist das Protokoll auf Commit/PR und einen Test (bUnit oder axe-Lauf), der den Fehler abdeckt
