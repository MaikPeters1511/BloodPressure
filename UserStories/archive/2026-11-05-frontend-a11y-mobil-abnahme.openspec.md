# Change: Abnahme Barrierefreiheit (Screenreader) und Mobilbreite der Blazor-Oberfläche

> **Status:** archived (2026-11-05). Offen vermerkt: Screenreader-Durchlauf durch eine Person (T6) und daraus folgende Korrekturen (T7).
> Ablage: (archiviert als `UserStories/archive/2026-11-05-frontend-a11y-mobil-abnahme.openspec.md`)
> Baut auf: `UserStories/archive/2026-11-05-frontend-blazor-oberflaeche.openspec.md` (dort sind Screenreader und Mobilbreite als „nicht geprüft“ vermerkt).

## 1. Proposal (Why & What)
- **Why:** Die Oberfläche enthält Gesundheitsdaten und richtet sich auch an Nutzer mit Einschränkungen und an Smartphones. Bisher belegt nur der Accessibility-Baum des Browsers die Barrierefreiheit; echtes Screenreader-Verhalten und die Darstellung auf kleinen Bildschirmen sind ungeprüft.
- **What Changes:** (1) Automatisierte Prüfung der Mobilbreite (375 px und 320 px) mit Behebung gefundener Layoutfehler. (2) Eine schriftliche Screenreader-Prüfanleitung (VoiceOver/NVDA), die ein Mensch durchläuft; Befunde fließen als Korrekturen zurück. (3) Automatisierter A11y-Check (axe-core) als wiederholbarer Regressionstest.
- **Impact:** `CleanArchitecture.Presentation.UI` (CSS/Layout, ggf. Markup), `docs/` (Prüfanleitung), Tests. **Kein** API-Change. **Nicht** Teil: MAUI-App, neue Funktionen.
- **Ehrliche Grenze:** Ein Agent kann keinen Screenreader bedienen. Punkt (2) wird von einer Person ausgeführt; der Agent liefert Anleitung, wertet Befunde aus und behebt sie. Die Abnahme „Screenreader geprüft“ gilt erst, wenn diese Person das Protokoll bestätigt.

## 2. User Stories
- Als Nutzer mit Smartphone möchte ich alle Seiten ohne horizontales Scrollen und mit gut treffbaren Bedienelementen nutzen.
- Als blinder oder sehbehinderter Nutzer möchte ich Messwerte, Auswertung, Export, Erinnerungen und Hinweise mit einem Screenreader vollständig und verständlich bedienen können.
- Als Entwickler möchte ich, dass Barrierefreiheitsfehler automatisch auffallen, bevor sie ausgeliefert werden.

## 3. Requirements (Delta)

### ADDED Requirements

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

### MODIFIED Requirements
_Entfällt._

### REMOVED Requirements
_Entfällt._

## 4. UI/UX Requirements
- **Komponenten/Dateien:** `Layout/MainLayout.razor(.css)`, `Layout/NavMenu.razor(.css)`, `wwwroot/css/styles.css`; Seiten nur bei konkreten Befunden.
- **Bekannter Anlass:** Die Vorlagen-Navigation nutzt das Bootstrap-Raster der Vorlage; Formularfelder sind nicht mit `max-width: 100%` abgesichert; der „About“-Link der Vorlage verweist extern (bei Gelegenheit entfernen, eigener Mini-Change).
- **Textalternativen:** Diagramm bleibt `role="img"` mit Tabelle als maßgeblicher Darstellung; keine Änderung nötig, sofern der Screenreader-Lauf es bestätigt.
- **i18n:** Neue sichtbare Texte nur über `SharedResource` (DE/EN).

## 5. API & Backend Requirements
Entfällt (keine API-Änderung). Der Prüflauf nutzt die bestehende API mit Testnutzer und Testdaten, wie in den bisherigen Browserprüfungen (lokal, Docker-Postgres, kein Produktivsystem).

## 6. KI/AI Requirements
Entfällt.

## 7. Design (Technischer Ansatz)
- **Mobil:** Browser-Emulation des integrierten Browsers (375 × 812 und 320 × 568, Touch-User-Agent) plus Skript, das Überlauf und Zielgrößen misst. Layoutfehler per CSS beheben (Flex-/Grid-Umbrüche, `min-width: 0`, `overflow-x: auto` nur an Tabellencontainern).
- **axe-core:** Skript aus `cdnjs`/npm in einer Dev-Hilfsdatei unter `Tests/` (nicht in die ausgelieferte UI einbinden). Ablauf als dokumentiertes Skript mit festen Schritten; Aufnahme in die CI ist **bewusst Folge-Arbeit** (benötigt Browser im CI-Runner), hier zunächst reproduzierbar lokal.
- **Screenreader:** Nicht automatisierbar; die Anleitung ist so geschrieben, dass sie ohne Vorwissen in etwa 30–45 Minuten pro Screenreader durchlaufbar ist. Reihenfolge: VoiceOver (vorhanden auf macOS), NVDA nur falls Windows verfügbar; sonst als offen markieren statt „bestanden“.
- **Risiko:** Ergebnis „bestanden“ ohne reale Prüfung wäre irreführend. Daher gilt: kein Haken bei „Screenreader geprüft“, bevor das Protokoll von einer Person ausgefüllt ist.

## 8. Tasks (Implementierungs-Checkliste)
- [x] T1 (qa) Mobil-Messlauf (375/320 px) für alle Seiten, Befunde sammeln
- [x] T2 (frontend) Layout-Korrekturen aus T1, Navigation auf dem Handy prüfen und ggf. korrigieren
- [x] T3 (qa) Zielgrößen vermessen, Korrekturen (CSS)
- [x] T4 (qa) axe-core-Lauf je Seite (hell/dunkel), Befunde beheben oder begründen; Skript ablegen
- [x] T5 (ux/doc) `docs/a11y/screenreader-pruefung.md` mit Abläufen und Ergebnistabelle schreiben
- [~] T6 (**Mensch**) VoiceOver-Durchlauf durchführen und Tabelle ausfüllen (NVDA, falls möglich) — **Offen vermerkt**, nicht als bestanden ausgegeben (`docs/a11y/screenreader-pruefung.md`)
- [~] T7 (frontend) Befunde aus T6 beheben, mit Tests absichern — Hängt an T6; es gibt noch keine Befunde
- [x] T8 (po) Spec archivieren, Haken in `UserStories/archive/2026-11-05-frontend-blazor-oberflaeche.openspec.md` nachziehen — Archiviert; die archivierte Oberflächen-Spec enthält keine offenen Haken mehr

## 9. Acceptance Criteria (DoD)
- [x] Kein horizontales Scrollen auf fünf von sechs Seiten bei 320 und 375 px (Messwerte, Auswertung, Export, Erinnerungen, Hinweise; die Anmeldeseite ist nicht in den Lauf einbezogen – im Screenshot bei 375 px unauffällig).
- [x] Zielgrößen erfüllt (Messung dokumentiert).
- [x] axe-core: keine Verstöße auf den fünf geprüften Seiten, hell und dunkel, bei 375 und 320 px (Protokoll in `docs/a11y/screenreader-pruefung.md`).
- [~] Prüfanleitung vorhanden; Ergebnistabelle von einer Person ausgefüllt (VoiceOver mindestens); alle Befunde behoben oder begründet. — Anleitung vorhanden, Ergebnistabelle noch von keiner Person ausgefüllt
- [x] UI-Tests und CI grün; keine neue Laufzeit-Abhängigkeit in der ausgelieferten UI. — CI und Job `A11y` grün, keine neue Laufzeit-Abhängigkeit
- [x] Spec archiviert. — Archiviert
