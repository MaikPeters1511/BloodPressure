# Capability: Profil und Einstellungen (Darstellung, Alter, Größe, Gewicht)

> Dauerhafte Capability-Spec. Quelle: archivierte Änderung `UserStories/archive/2026-11-05-profil-einstellungen.openspec.md`.
> Offen: Screenreader-Durchlauf durch eine Person (Web: VoiceOver/NVDA, MAUI: VoiceOver/TalkBack) laut `docs/a11y/`; Windows-Build der MAUI-App nicht geprüft.

## Requirements

#### Requirement: Darstellung wählen
Das System SHALL auf der Seite **Profil & Einstellungen** die Darstellung *System*, *Hell* und *Dunkel* anbieten, sofort anwenden und je Gerät lokal speichern (Web: `localStorage`; MAUI: `Preferences`); ohne gespeicherte Wahl gilt *System*. Die Wahl SHALL NOT an den Server gesendet werden.

##### Scenario: Dunkel wählen
- **GIVEN** das Systemschema ist hell
- **WHEN** der Nutzer „Dunkel“ wählt
- **THEN** wechselt die Oberfläche sofort in die dunkle Darstellung und bleibt nach einem Neuladen bzw. App-Neustart dunkel

##### Scenario: Zurück auf System
- **GIVEN** „Dunkel“ ist gewählt
- **WHEN** der Nutzer „System“ wählt
- **THEN** folgt die Oberfläche wieder dem Schema des Geräts, auch bei dessen späterem Wechsel

##### Scenario: Speicher nicht verfügbar
- **GIVEN** der lokale Speicher ist blockiert (z. B. privates Fenster)
- **WHEN** der Nutzer eine Darstellung wählt
- **THEN** gilt sie bis zum Neuladen, und die Seite bleibt bedienbar (kein Fehlerdialog)

#### Requirement: Profildaten erfassen
Das System SHALL Alter (ganze Jahre, 0–120), Größe (cm, 50–250, ganze Zahl) und Gewicht (kg, 2–500, höchstens eine Nachkommastelle) je Nutzer speichern. Jede Angabe ist optional und einzeln löschbar. Gespeichert wird statt des Alters das **Geburtsjahr** (aktuelles Jahr minus Alter, zum Speicherzeitpunkt); angezeigt wird das daraus berechnete Alter. Die Daten gehören dem angemeldeten Nutzer und sind für andere Nutzer nicht lesbar.

##### Scenario: Erstmals speichern
- **GIVEN** ein Nutzer ohne Profil
- **WHEN** er Alter 42, Größe 180 und Gewicht 82,5 speichert
- **THEN** liefert `GET /api/v1/profile` Alter 42, Größe 180, Gewicht 82,5 und die Seite zeigt sie nach dem Neuladen an

##### Scenario: Ungültige Eingabe
- **GIVEN** Größe 20 cm
- **WHEN** gespeichert wird
- **THEN** antwortet die API mit 400 und Feldmeldung `heightCm`; die Seite zeigt die Meldung am Feld, ohne Daten zu überschreiben

##### Scenario: Angabe löschen
- **GIVEN** ein gespeichertes Gewicht
- **WHEN** der Nutzer das Feld leert und speichert
- **THEN** ist das Gewicht gelöscht und der BMI verschwindet

##### Scenario: Alter wird mit den Jahren korrekt
- **GIVEN** Geburtsjahr 1984 gespeichert
- **WHEN** das Profil im Jahr 2027 gelesen wird
- **THEN** beträgt das Alter 43

##### Scenario: Fremde Daten
- **GIVEN** Nutzer A hat ein Profil
- **WHEN** Nutzer B `GET /api/v1/profile` aufruft
- **THEN** sieht B nur sein eigenes (leeres) Profil; ohne Anmeldung antwortet die API mit 401

#### Requirement: BMI anzeigen
Das System SHALL aus Größe und Gewicht den BMI (kg / m², eine Nachkommastelle) berechnen und anzeigen, sofern beide Angaben vorliegen, mit dem Hinweis „Dies ist keine Diagnose“. Das System SHALL keine Kategorie und keine Wertung anzeigen.

##### Scenario: BMI berechnen
- **GIVEN** Größe 180 cm und Gewicht 82,5 kg
- **WHEN** die Seite angezeigt wird
- **THEN** steht dort „BMI 25,5“ (lokalisiertes Zahlenformat) und der Hinweis

##### Scenario: Angabe fehlt
- **GIVEN** nur die Größe ist gespeichert
- **WHEN** die Seite angezeigt wird
- **THEN** erscheint kein BMI

#### Requirement: Profil löschen
Das System SHALL dem Nutzer ermöglichen, alle Profilangaben mit Bestätigung zu löschen (`DELETE /api/v1/profile`).

##### Scenario: Löschen bestätigen
- **GIVEN** ein gespeichertes Profil
- **WHEN** der Nutzer „Profildaten löschen“ wählt und bestätigt
- **THEN** sind alle Angaben am Server gelöscht und die Felder leer; ohne Bestätigung passiert nichts

#### Requirement: Datenschutz
Das System SHALL Alter, Größe und Gewicht nicht in Logs, Fehlermeldungen, Exporten (CSV/PDF), Hinweistexten oder Prompts an externe Dienste ausgeben und nur über die gesicherten Endpunkte des angemeldeten Nutzers bereitstellen.

##### Scenario: Export
- **GIVEN** ein Nutzer mit Profil
- **WHEN** er CSV oder PDF exportiert
- **THEN** enthält die Datei keine Profilangaben

#### Requirement: Navigation, Sprache, Barrierefreiheit
Das System SHALL die Seite in der Navigation (Web) bzw. als Tab/Menüeintrag (MAUI) erreichbar machen, alle Texte über `SharedResource`/`Strings` (DE/EN) ausgeben, Eingaben beschriften (Einheit im Label, z. B. „Größe (cm)“), Fehler und Erfolg ansagen (`role=alert`/`status` bzw. `SemanticScreenReader`) und die Auswahl der Darstellung ohne Farbe als alleinigen Hinweis erkennbar machen (Auswahl als Radiogruppe mit Text).

##### Scenario: Tastatur und Screenreader
- **GIVEN** die Seite ist geöffnet
- **WHEN** der Nutzer nur mit Tastatur bzw. Screenreader navigiert
- **THEN** sind alle Eingaben und Schaltflächen erreichbar, haben Namen, und das Speichern-Ergebnis wird angesagt

### MODIFIED Requirements
- **Farbschemata der Web-Oberfläche:** Bisher folgt das Aussehen ausschließlich `prefers-color-scheme`. Neu: ein Attribut `data-theme="light|dark"` am `<html>`-Element überschreibt das Systemschema; ohne Attribut gilt wie bisher das Systemschema. Alle in `styles.css` per Media-Query gesetzten Farben (Diagramm, Tabellen, Schaltflächen, Seitenleiste) müssen für beide Wege (Attribut und System) gleich gelten.

### REMOVED Requirements
_Entfällt._
