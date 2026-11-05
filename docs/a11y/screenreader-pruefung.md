# Barrierefreiheits-Prüfung der Blazor-Oberfläche

Spec: `UserStories/frontend-a11y-mobil-abnahme.openspec.md`. Dieses Dokument hat zwei Teile: die **automatisierte Prüfung** (wiederholbar, Browser) und die **Screenreader-Prüfung** (von einer Person auszuführen und zu protokollieren).

## 1. Umgebung vorbereiten (lokal, kein Produktivsystem)

1. Datenbank: `docker run -d --rm --name bp-pg -e POSTGRES_PASSWORD=<selbst gewählt> -e POSTGRES_DB=bloodpressure -p 55432:5432 postgres:17`
2. API (Development, Migrationen laufen automatisch):
   `ConnectionStrings__bloodpressuredb="Host=localhost;Port=55432;Database=bloodpressure;Username=postgres;Password=<wie oben>" Cors__AllowedOrigins__0="http://localhost:5296" ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:5000" dotnet run --project CleanArchitecture.Presentation.API --no-launch-profile`
3. UI: in `CleanArchitecture.Presentation.UI/wwwroot/appsettings.json` lokal `Api:BaseUrl` auf `http://localhost:5000` setzen (**nicht committen**), dann
   `dotnet run --project CleanArchitecture.Presentation.UI --no-launch-profile --urls http://localhost:5296`
4. Testnutzer registrieren (E-Mail `ui@example.test`, selbst gewähltes Passwort, nicht ins Repository schreiben) und Testdaten anlegen: mindestens 12 Messungen verteilt auf mehrere Tage, eine Messung mit 195/120 (löst einen Hinweis aus), Erinnerungen 08:00 und 20:00.

## 2. Automatisierte Prüfung (axe-core, Mobilbreite, Zielgrößen)

**In der CI:** Der Job `A11y` in `.github/workflows/ci.yml` startet Postgres, API und UI, legt einen Testnutzer mit Testdaten an und führt `Tests/a11y/check.mjs` (Playwright + `@axe-core/playwright`) aus: 5 Seiten × 3 Breiten (1280, 375, 320 px) × 2 Farbschemata = 30 Prüfungen. Bei Fehlern lädt der Job `a11y-results.json` und die Logs als Artefakt hoch.

**Lokal:** Umgebung wie in Abschnitt 1 starten, dann
`cd Tests/a11y && npm ci && npx playwright install chromium && node check.mjs`
(mit `A11Y_CHROMIUM=<Pfad>` lässt sich ein vorhandener Chromium verwenden). Alternativ liefert `Tests/a11y/axe-run.js` einen schnellen Einzellauf in der Browser-Konsole.

Zulässig: keine Verstöße der Schwere *serious/critical*, `overflow: false`, keine Bedienelemente unter 24 × 24 px.

| Datum | Stand (Commit) | 375 hell | 375 dunkel | 320 hell | 320 dunkel | Bemerkung |
|---|---|---|---|---|---|---|
| 2026-11-04 | Branch `feature/ui-mobile-a11y` (manuell per Konsole) | ohne Befund | ohne Befund | ohne Befund | ohne Befund | Seiten: Messwerte, Auswertung, Export, Erinnerungen, Hinweise; Anmeldeseite nicht Teil des Laufs. axe-core 4.10.2 findet nur einen Teil der Probleme, er ersetzt den Screenreader-Test nicht. |

## 3. Screenreader-Prüfung (manuell)

**Werkzeuge:** VoiceOver (macOS, Safari) – Start/Stop mit `Cmd + F5`; NVDA (Windows, Firefox oder Chrome). Dauer etwa 30–45 Minuten je Screenreader.

**VoiceOver-Grundbedienung:** `Ctrl + Option + →/←` nächstes/vorheriges Element, `Ctrl + Option + Leertaste` aktivieren, `Ctrl + Option + U` Rotor (Überschriften, Formularfelder, Tabellen, Orientierungspunkte). **NVDA:** `↓/↑` lesen, `Tab` Steuerelemente, `H` Überschriften, `T` Tabellen, `D` Orientierungspunkte, `Einfg + F7` Elementliste.

Je Ablauf notieren: **bestanden**, oder **Befund** (Seite, Schritt, gehört, erwartet).

| # | Seite | Ablauf (Schritte) | Erwartete Ansage / Verhalten |
|---|---|---|---|
| 1 | Anmeldung | Falsches Passwort absenden. | Fehlermeldung wird automatisch angesagt (`role=alert`), Fokus bleibt bedienbar; Felder heißen „E-Mail“, „Passwort“. |
| 2 | Navigation | Mit dem Rotor Orientierungspunkte öffnen; Menüknopf (Handybreite) bedienen. | Bereich „Hauptnavigation“; Menüknopf sagt „Hauptnavigation, Schaltfläche, ausgeklappt/eingeklappt“; nach Auswahl eines Eintrags klappt das Menü zu. |
| 3 | Navigation | Zähler „Hinweise“ lesen. | „Hinweise, 1 ungelesener Hinweis“ (bei mehreren: „n ungelesene Hinweise“). |
| 4 | Messwerte | Messung mit ungültigem Wert (z. B. Systolisch 999) anlegen. | Validierungsfehler wird angesagt; jedes Feld hat seine Beschriftung. |
| 5 | Messwerte | Liste per Tabellennavigation lesen. | Tabelle mit Beschriftung „Meine Blutdruckwerte“; Spaltenüberschriften werden je Zelle genannt; die **Kategorie steht als Text** („Hypertonie Grad 3“), nicht nur als Farbe. |
| 6 | Messwerte | „Löschen“ wählen, bestätigen. | Schaltfläche nennt das Datum der Messung; Bestätigungsfrage wird gelesen. |
| 7 | Auswertung | Zum Diagramm navigieren. | Bild mit Beschreibung („Verlauf der Durchschnittswerte … Werte stehen in der Tabelle darunter“); anschließende Tabelle „Kennzahlen je Zeitabschnitt“ ist vollständig lesbar. Der Screenreader liest nicht jede Achsenzahl einzeln vor. |
| 8 | Auswertung | „Von“ nach „Bis“ legen, anwenden. | Meldung „Von darf nicht nach Bis liegen“ wird angesagt, keine Anfrage. |
| 9 | Export | CSV herunterladen. | Während des Vorgangs „Datei wird erstellt …“, danach „Die Datei … wurde heruntergeladen.“ (`role=status`). |
| 10 | Erinnerungen | Zeitzone leeren, speichern. | Fehler wird angesagt, **Fokus springt in den Fehlerbereich**. |
| 11 | Erinnerungen | Uhrzeit entfernen. | Fokus landet auf „Uhrzeit hinzufügen“, nicht auf der Seite verloren. Schaltflächen heißen „Uhrzeit 2 entfernen“. |
| 12 | Hinweise | Ungelesene Warnung lesen, „Als gelesen markieren“ wählen. | Warnung wird beim Laden angesagt; nach dem Markieren Ansage „Der Hinweis wurde als gelesen markiert.“, **Fokus liegt auf dem Hinweis**, Zähler in der Navigation sinkt. |
| 14 | Profil | Darstellung wählen, Alter/Größe/Gewicht eingeben, speichern. | Optionsgruppe „Darstellung“ wird als Gruppe angesagt, jede Option mit „ausgewählt/nicht ausgewählt“; Felder heißen „Alter (Jahre)“, „Größe (cm)“, „Gewicht (kg)“; nach dem Speichern „Das Profil wurde gespeichert.“; ungültige Eingabe: Fokus im Fehlerbereich; BMI-Zeile meldet sich höflich („BMI 25,5 Dies ist keine Diagnose.“). |
| 13 | Alle | Überschriftenstruktur prüfen (Rotor/`H`). | Eine `h1` je Seite, sinnvolle Reihenfolge, Seitentitel entspricht der Seite. |

### Ergebnisprotokoll (bitte ausfüllen)

| Datum | Prüfer | Screenreader / Browser / System | Version der Anwendung (Commit) | Ergebnis je Ablauf (1–13) | Befunde |
|---|---|---|---|---|---|
| _offen_ | _offen_ | VoiceOver / Safari / macOS | | | |
| _offen_ | _offen_ | NVDA / Firefox / Windows | | | |

**Status:** Screenreader-Prüfung **nicht durchgeführt**. Die Abnahme „Screenreader geprüft“ gilt erst, wenn mindestens die VoiceOver-Zeile von einer Person ausgefüllt ist und alle Befunde behoben oder begründet sind.

Der automatisierte Lauf (`check.mjs`, jetzt auch 1280 px) fand zusätzlich: Seitenwechsel-Leiste lief bei 320 px über (umbrechbar gemacht), Auswahlfelder am Desktop 21 px hoch, Seitenleisten-Links im hellen Schema zu kontrastarm. Alle behoben; Stand 30/30 Prüfungen bestanden.
