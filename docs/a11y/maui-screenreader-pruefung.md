# Barrierefreiheits-Prüfung der MAUI-App

Spec: `UserStories/maui-app-api-anbindung.openspec.md` (Phase 4). Zwei Teile: die **automatisierte Prüfung** (läuft in der CI) und die **Screenreader-Prüfung auf dem Gerät** (von einer Person auszuführen und zu protokollieren). Für die Web-Oberfläche gilt `docs/a11y/screenreader-pruefung.md`.

## 1. Automatisierte Prüfung

`Tests/CleanArchitecture.Maui.Tests/XamlAccessibilityTests.cs` liest alle `Views/*.xaml` und prüft bei jedem CI-Lauf:

- jedes Eingabeelement (`Entry`, `Picker`, `DatePicker`, `TimePicker`, `Switch`) hat eine `SemanticProperties.Description`;
- jede Schaltfläche hat sichtbaren Text oder eine Beschreibung;
- Eingabefelder und Schaltflächen sind mindestens 44 pt hoch (`MinimumHeightRequest`);
- Überschriften (`Headline`/`SubHeadline`) tragen eine `HeadingLevel`; jede Seite hat einen `Title` und genau eine Überschrift der Ebene 1;
- keine hartcodierten sichtbaren Texte (alle über `{loc:Translate …}`);
- die Kategorie-Badges haben neben der Farbe einen Text.

Die Prüfung wurde per Gegenprobe validiert (absichtlich eingebaute Fehler werden gefunden). Sie ersetzt **keinen** Screenreader-Test: sie sagt nichts über Lesereihenfolge, Ansagen oder Fokusführung.

### Accessibility-Baum der Mac-Catalyst-App (Stichprobe 2026-11-05, macOS 27)

Anmeldeseite, geprüft über die Bedienungshilfen-Schnittstelle: Eingabefelder „E-Mail“ und „Passwort“ (als sicheres Textfeld), Schaltflächen „Anmelden“ und „Registrieren“ sind mit Namen und Rolle vorhanden und 333 × 34 pt groß (Mac-Darstellung); zwei Überschriften „Anmelden“ (Navigationsleiste und Seitenüberschrift). Die doppelte Überschrift ist eine Eigenheit der Shell (Seitentitel plus `h1`) und kein Fehler, wird aber von Screenreadern zweimal vorgelesen. Die übrigen Seiten ließen sich nicht prüfen, weil die Anmeldung (Passwortfeld) nur von einer Person bedient werden kann.

## 2. Vorbereitung für die Geräteprüfung

1. API und Datenbank lokal starten (siehe `docs/a11y/screenreader-pruefung.md`, Abschnitt 1); Testnutzer registrieren (selbst gewähltes Passwort, nicht ins Repository schreiben).
2. Testdaten anlegen: mindestens 12 Messungen über mehrere Tage, eine Messung mit 195/120 (löst einen Hinweis aus), Erinnerungen 08:00 und 20:00.
3. App bauen und starten: iOS-Simulator oder Gerät (`dotnet build CleanArchitecture.Presentation.Maui -f net10.0-ios …`), Android-Emulator oder Gerät mit erreichbarer API (Debug: Emulator-Adresse `10.0.2.2` ist eingestellt).
4. Screenreader einschalten: **VoiceOver** (iOS: Einstellungen › Bedienungshilfen; macOS: `Cmd + F5`), **TalkBack** (Android: Einstellungen › Bedienungshilfen).

Grundbedienung VoiceOver: Wischen rechts/links = nächstes/vorheriges Element, Doppeltippen = aktivieren. TalkBack: gleiche Gesten.

## 3. Abläufe

Je Ablauf notieren: **bestanden** oder **Befund** (Seite, Schritt, gehört, erwartet).

| # | Seite | Ablauf | Erwartete Ansage / Verhalten |
|---|---|---|---|
| 1 | Anmeldung | Mit leeren Feldern „Anmelden“ wählen. | Fehlermeldung „Bitte gib E-Mail und Passwort ein.“ wird automatisch angesagt; Felder heißen „E-Mail“, „Passwort“. |
| 2 | Anmeldung | Falsches Passwort absenden. | „E-Mail oder Passwort ist falsch.“ wird angesagt. |
| 3 | Navigation | Tab-Leiste durchgehen. | Fünf Tabs: Messwerte, Auswertung, Export, Erinnerungen, Hinweise; bei ungelesenen Hinweisen „Hinweise (n)“; aktiver Tab wird als ausgewählt angesagt. |
| 4 | Messwerte | Liste durchgehen. | Pro Eintrag eine vollständige Ansage: Datum, Werte in mmHg, Puls, Sauerstoff **und Kategorie als Text** (z. B. „Bluthochdruck“). Farbe ist nie die einzige Information. Schaltfläche „Löschen“ ist erreichbar. |
| 5 | Messwerte | „Neue Messung hinzufügen“ wählen, Systolisch 999 eingeben, speichern. | Formular klappt auf (Schaltfläche heißt danach „Formular schließen“); Validierungsfehler wird angesagt. Felder heißen „Systolisch“, „Diastolisch“, „Puls“, „Sauerstoff (%)“, „Datum“, „Uhrzeit“. |
| 6 | Messwerte | Gültige Messung speichern. | Ansage „Die Messung wurde gespeichert.“; Formular schließt, Liste zeigt den neuen Eintrag. |
| 7 | Messwerte | „Löschen“ wählen. | Bestätigungsdialog mit Datum der Messung; „Abbrechen“ und „Löschen“ erreichbar. |
| 8 | Auswertung | Zum Diagramm navigieren. | Beschreibung „Verlauf der Durchschnittswerte … Die Werte stehen in der Tabelle darunter.“; die Legende nennt die Reihen mit Namen; jede Zeile der Wertetabelle wird vollständig vorgelesen („… 2 Messungen. Systolisch … Diastolisch … Puls …“). |
| 9 | Auswertung | „Von“ nach „Bis“ legen, „Anwenden“. | „„Von“ darf nicht nach „Bis“ liegen.“ wird angesagt, keine Anfrage. |
| 10 | Export | CSV exportieren. | Während des Vorgangs „Datei wird erstellt …“, dann Teilen-Dialog; danach Ansage „Die Datei blutdruck-alle-alle.csv wurde bereitgestellt.“ |
| 11 | Erinnerungen | Zeiten lesen, eine hinzufügen, entfernen. | Zeitwähler heißen „Uhrzeit 1“, „Uhrzeit 2“ …, Entfernen-Schaltflächen „Uhrzeit 2 entfernen“; nach dem Entfernen Ansage „Uhrzeit 2 wurde entfernt.“; „Uhrzeit hinzufügen“ ist bei fünf Zeiten deaktiviert. |
| 12 | Erinnerungen | Doppelte Uhrzeit speichern. | „Jede Uhrzeit darf nur einmal vorkommen.“ wird angesagt. |
| 13 | Hinweise | Warnung lesen, „Als gelesen markieren“. | Ansage der Warnung: „Warnung Bluthochdruck. Neu. …“; nach dem Markieren „Der Hinweis wurde als gelesen markiert.“, Zähler im Tab sinkt. |
| 16 | Profil | „Profil“ in der Navigationsleiste der Messwerte öffnen; Darstellung wechseln; Werte eingeben und speichern. | Optionen „System/Hell/Dunkel“ als Optionsfelder mit Zustand; Ansage „Darstellung: Dunkel“ beim Wechsel; Felder „Alter (Jahre)“, „Größe (cm)“, „Gewicht (kg)“; „Das Profil wurde gespeichert.“; BMI mit „Dies ist keine Diagnose.“; „Profildaten löschen“ fragt nach. |
| 14 | Alle | Schriftgröße auf das Maximum stellen (iOS: Dynamische Schrift; Android: Schriftgröße/Anzeigegröße). | Nichts wird abgeschnitten, alle Seiten bleiben bedienbar (Scrollen möglich). |
| 15 | Alle | Dunkler Modus, Querformat. | Texte und Badges bleiben lesbar; Bedienelemente erreichbar. |

### Ergebnisprotokoll (bitte ausfüllen)

| Datum | Prüfer | Screenreader / System / Gerät | Version (Commit) | Ergebnis je Ablauf (1–15) | Befunde |
|---|---|---|---|---|---|
| _offen_ | _offen_ | VoiceOver / iOS | | | |
| _offen_ | _offen_ | TalkBack / Android | | | |
| _offen_ | _offen_ | VoiceOver / macOS (Mac Catalyst) | | | |

**Status:** Geräteprüfung **nicht durchgeführt**. „Screenreader geprüft“ gilt erst, wenn mindestens die VoiceOver-Zeile (iOS) von einer Person ausgefüllt ist und alle Befunde behoben oder begründet sind. Android/TalkBack bleibt offen, solange der Android-Build ungeprüft ist.
