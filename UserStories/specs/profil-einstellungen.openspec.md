# Capability: Profil und Einstellungen (Darstellung, Alter, Größe, Gewicht)

> Dauerhafte Capability-Spec. Quelle: archivierte Änderung `UserStories/archive/2026-11-05-profil-einstellungen.openspec.md`.
> Erweitert um die Sprachwahl (archivierte Änderung `UserStories/archive/2026-11-05-sprache-einstellungen.openspec.md`).
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

#### Requirement: Sprache wählen
Das System SHALL auf der Seite **Profil & Einstellungen** die Sprachen *System*, *Deutsch* und *English* anbieten, die Wahl sofort anwenden und je Gerät lokal speichern (Web: `localStorage`, MAUI: `Preferences`); ohne gespeicherte Wahl gilt *System*. Bei *System* gilt die Browser- bzw. Gerätesprache: Englisch, wenn sie mit `en` beginnt, sonst Deutsch. Die Wahl SHALL NOT an den Server gesendet werden. Die Einträge heißen in jeder Oberflächensprache gleich („System“ wird übersetzt, „Deutsch“ und „English“ stehen in ihrer eigenen Sprache und tragen ein passendes `lang`-Attribut bzw. `SemanticProperties`-Sprachhinweis, soweit die Plattform es zulässt).

##### Scenario: Auf Englisch wechseln
- **GIVEN** die Oberfläche ist deutsch
- **WHEN** der Nutzer „English“ wählt
- **THEN** erscheinen alle sichtbaren Texte der Seite sofort auf Englisch und bleiben nach Neuladen bzw. App-Neustart englisch

##### Scenario: Zurück auf System
- **GIVEN** „English“ ist gewählt, das Gerät steht auf Deutsch
- **WHEN** der Nutzer „System“ wählt
- **THEN** wechselt die Oberfläche auf Deutsch, und die gespeicherte Wahl wird entfernt

##### Scenario: Gerätesprache ohne Wahl
- **GIVEN** keine gespeicherte Wahl und Browser-/Gerätesprache `fr`
- **WHEN** die App startet
- **THEN** erscheint die Oberfläche auf Deutsch

##### Scenario: Speicher nicht verfügbar
- **GIVEN** der lokale Speicher ist blockiert (z. B. privates Fenster)
- **WHEN** der Nutzer eine Sprache wählt
- **THEN** gilt sie bis zum Neuladen, und die Seite bleibt bedienbar (kein Fehlerdialog)

#### Requirement: Sprachwechsel wirkt auf die ganze Oberfläche
Das System SHALL beim Sprachwechsel alle sichtbaren Texte (Navigation, Seitentitel, Tab-Titel, Meldungen, Dialoge, Statusansagen), die Zahlen- und Datumsformate (z. B. BMI „25,5“ / „25.5“, Datum und Uhrzeit) und in der Web-Oberfläche das `lang`-Attribut von `<html>` auf die gewählte Sprache umstellen, ohne dass der Nutzer sich erneut anmelden muss.

##### Scenario: Navigation und andere Seiten
- **GIVEN** der Nutzer hat auf „English“ gewechselt
- **WHEN** er zu Messwerten, Auswertung, Export, Erinnerungen oder Hinweisen navigiert
- **THEN** sind Seitentitel, Beschriftungen, Kategorien und Meldungen englisch

##### Scenario: Zahlenformat
- **GIVEN** Gewicht 82,5 ist gespeichert
- **WHEN** der Nutzer auf „English“ wechselt
- **THEN** zeigt das Gewichtsfeld „82.5“ und der BMI „25.5“; beim Wechsel zurück „82,5“ und „25,5“; beide Schreibweisen bleiben bei der Eingabe zulässig

##### Scenario: Ungespeicherte Eingaben bleiben (nur Web; in der App gehen sie beim Neuaufbau der Oberfläche verloren)
- **GIVEN** der Nutzer hat im Profil Alter und Größe eingegeben, aber noch nicht gespeichert
- **WHEN** er die Sprache wechselt
- **THEN** bleiben die eingegebenen Werte erhalten (nur die Formatierung passt sich an)

##### Scenario: Sprache der Seite (Web)
- **GIVEN** „English“ ist gewählt
- **WHEN** die Seite gerendert wird
- **THEN** steht `<html lang="en">`, bei Deutsch `lang="de"`

#### Requirement: Sprachwechsel in der MAUI-App
Das System SHALL in der MAUI-App nach dem Wechsel die Oberfläche (Shell mit Tab-Titeln und die geöffneten Seiten) in der neuen Sprache neu aufbauen und danach wieder die Profilseite anzeigen; wo die Plattform dies nicht zulässt, SHALL die App einen deutlichen Hinweis auf den nötigen Neustart anzeigen statt scheinbar unverändert zu bleiben.

##### Scenario: Tab-Titel
- **GIVEN** die App ist deutsch
- **WHEN** der Nutzer „English“ wählt
- **THEN** heißen die Tabs „Readings“, „Statistics“, „Export“, „Reminders“, „Notices“ (der Zähler ungelesener Hinweise bleibt erhalten), und der Nutzer bleibt angemeldet

#### Requirement: Barrierefreiheit der Sprachwahl
Das System SHALL die Sprachwahl als Gruppe mit sichtbarer Überschrift „Sprache“ bereitstellen (Web: `fieldset`/`legend` mit Radiobuttons; MAUI: gruppierte `RadioButton`s mit `SemanticProperties`), per Tastatur bedienbar, mit Zielgröße von mindestens 24 × 24 px, und den Wechsel **in der neuen Sprache** über eine Statusansage (Web: `aria-live`; MAUI: `SemanticScreenReader.Announce`) mitteilen.

##### Scenario: Statusansage
- **GIVEN** ein Screenreader ist aktiv
- **WHEN** der Nutzer die Sprache wechselt
- **THEN** wird einmalig „Sprache: English“ bzw. „Sprache: Deutsch“ angesagt, und der Fokus bleibt auf dem gewählten Eintrag

##### Scenario: Automatisierte Prüfung
- **GIVEN** die Profilseite mit dem Abschnitt „Sprache“
- **WHEN** der axe-core-Lauf (hell/dunkel, 1280/375/320 px, deutsch und englisch) und der statische XAML-Test laufen
- **THEN** gibt es keine Verstöße der Schwere „serious/critical“
