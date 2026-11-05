# Change: Sprache in „Profil & Einstellungen“ umstellen (Deutsch / Englisch)

> **Status:** archived (2026-11-05). Offen vermerkt: Screenreader-Durchlauf durch eine Person (Abläufe 15 und 17), Mac Catalyst und Windows nicht geprüft. Der iOS-Simulator wurde nachträglich geprüft (siehe T5).
> Ablage: (archiviert als `UserStories/archive/2026-11-05-sprache-einstellungen.openspec.md`; dauerhafte Requirements: `UserStories/specs/profil-einstellungen.openspec.md`)
> Baut auf: `UserStories/specs/profil-einstellungen.openspec.md` (Darstellung je Gerät, gleiche Ablage und gleiches Muster), Web-Oberfläche (`UserStories/specs/frontend-oberflaeche.openspec.md`) und MAUI-App (`UserStories/maui-app-api-anbindung.openspec.md`).

## 1. Proposal (Why & What)
- **Why:** Die Sprache richtet sich heute ausschließlich nach dem Browser (Web: `navigator.language`, nur `en`, sonst Deutsch) bzw. dem Gerät (MAUI). Nutzer können sie nicht selbst wählen, z. B. wenn das Gerät auf Englisch steht, die App aber Deutsch sein soll, oder umgekehrt.
- **What Changes:**
  1. Auf der Seite **Profil & Einstellungen** (Web und MAUI) kommt ein Abschnitt **Sprache** hinzu: *System*, *Deutsch*, *English* (Standard: *System*).
  2. Die Wahl gilt **je Gerät** und wird lokal gespeichert (Web: `localStorage`, MAUI: `Preferences`), **nicht** am Server – wie die Darstellung (Theme).
  3. Die Wahl wirkt ohne Neustart: alle sichtbaren Texte, Zahlen- und Datumsformate und das `lang`-Attribut der Seite wechseln.
- **Impact:** `CleanArchitecture.Presentation.UI` (Kultur-Start in `Program.cs`, `Profile.razor`, Sprachdienst, Layout), `CleanArchitecture.Presentation.Maui.Core` (`ILocalizer`, `ProfileViewModel`, Spracheinstellung), `CleanArchitecture.Presentation.Maui` (Shell/Seiten neu aufbauen, `Preferences`), Resx (DE/EN), Tests, Doku. **Kein** API-Change, **keine** Migration. **Nicht** Teil: weitere Sprachen als Deutsch/Englisch, serverseitige Sprache (die Texte der Hinweise entstehen im Client aus der Kategorie), Übersetzung nutzereigener Daten.

## 2. User Stories
- Als Nutzer möchte ich die Sprache der App unabhängig von der Geräte- oder Browsersprache zwischen Deutsch und Englisch wählen, damit ich die App in der Sprache nutze, die ich am besten verstehe.
- Als Nutzer möchte ich, dass die Wahl sofort wirkt und nach einem Neustart erhalten bleibt.
- Als Nutzer mit Screenreader möchte ich, dass der Wechsel angesagt wird und die Seite danach in der richtigen Sprache vorgelesen wird.

## 3. Requirements (Delta)

### ADDED Requirements

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

##### Scenario: Ungespeicherte Eingaben bleiben
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

### MODIFIED Requirements
_Entfällt._ (Das Standardverhalten ohne Wahl bleibt unverändert: Englisch bei `en*`, sonst Deutsch.)

### REMOVED Requirements
_Entfällt._

## 4. UI/UX Requirements
- **Komponenten (Web):** `Pages/Profile.razor` (neuer Abschnitt „Sprache“ über oder unter „Darstellung“), neuer `ILanguageService`/`LanguageService` (analog `ThemeService`: lesen/schreiben `localStorage` über `IBrowserInterop`, Kultur setzen, Änderungsereignis), `Program.cs` (Start: gespeicherte Wahl vor Browsersprache), `Layout/MainLayout.razor` (neu rendern bei Sprachwechsel, `lang`-Attribut per JS), ggf. `wwwroot/index.html` (Hilfsfunktion `setHtmlLang`).
- **Komponenten (MAUI):** `ProfileViewModel` (Eigenschaften `IsLanguageSystem/German/English`, Kommando analog Theme), neuer `LanguageSettings` im Core (über `ISettingsStore`, Schlüssel `bp.language`), `ResxLocalizer` mit wechselbarer Kultur (statt fester Kultur im Konstruktor), `ProfilePage.xaml` (RadioButton-Gruppe `language`), Neuaufbau der Shell (`AppShell` neu erzeugen und als Fensterinhalt setzen).
- **State:** Gewählte Sprache lokal je Gerät; im Web ein scoped Dienst mit Ereignis `Changed`, das das Layout zum Neuzeichnen bringt.
- **i18n / a11y:** Neue Schlüssel in `SharedResource(.en).resx` und `Strings(.en).resx` (z. B. `Language_Title`, `Language_System`, `Language_German`, `Language_English`, `Language_Changed` mit Platzhalter für den Sprachnamen, `Language_Note` „Gilt nur für dieses Gerät.“); DE/EN-Paritätstests müssen grün bleiben. `Culture_Name` bleibt die Quelle der Zahlenformat-Kultur im Web.

## 5. API & Backend Requirements
Entfällt. Kein Endpoint, kein Modell, keine Migration. Die Sprachwahl wird bewusst **nicht** am Server gespeichert (Gerätebezug, keine Netzabhängigkeit beim Start, keine zusätzlichen personenbezogenen Daten).

## 6. KI/AI Requirements
Entfällt.

## 7. Design (Technischer Ansatz)
- **Ablage:** Wie die Darstellung (siehe archivierte Profil-Spec): lokal je Gerät; Schlüssel `bp.lang` (Web, Werte `de`/`en`, fehlend = System) bzw. `bp.language` (MAUI).
- **Web – Start:** In `Program.cs` zuerst die gespeicherte Wahl lesen (über `IBrowserInterop`, mit try/catch bei blockiertem Speicher), sonst wie bisher die Browsersprache. Kultur auf `DefaultThreadCurrentCulture` und `DefaultThreadCurrentUICulture` setzen.
- **Web – Wechsel zur Laufzeit:** Kultur neu setzen und das Layout neu rendern (`StateHasChanged` über das Änderungsereignis; alle Seiten lesen `IStringLocalizer` bei jedem Rendern). Seiten mit berechneten Texten (Statistik-Beschriftungen, Zeitformate) müssen beim Ereignis neu berechnen. **Fallback**, falls ein sauberes Neurendern zu viele Sonderfälle erzeugt: `NavigationManager.NavigateTo(Uri, forceLoad: true)` auf die Profilseite; die Spec fordert nur das sichtbare Ergebnis („ohne erneute Anmeldung, Werte bleiben“), nicht den Mechanismus. Dann gehen ungespeicherte Eingaben verloren – das Scenario „Ungespeicherte Eingaben bleiben“ gilt nur für den Neurender-Weg und ist bei der Entscheidung zu berücksichtigen.
- **MAUI:** `loc:Translate` wertet statisch aus; ein Wechsel erfordert daher das **Neuerzeugen** der Shell. Der Lokalisierer wird als Singleton beibehalten und bekommt eine setzbare Kultur. Nach dem Neuaufbau wird zur Profilseite zurücknavigiert; Anmeldung (`SecureStorage`) bleibt unberührt. Ist das auf einer Plattform nicht zuverlässig, greift der Neustart-Hinweis (siehe Requirement).
- **Zahlenformat:** Die Anzeige folgt der Sprachwahl (nicht der Gerätekultur); Eingaben akzeptieren weiterhin Komma und Punkt (bestehendes Verhalten des Profils).
- **Risiken:** (1) Texte, die beim Seitenaufbau einmalig in Felder kopiert werden, bleiben alt – durch Tests je Seite (bUnit: Sprache wechseln, Text prüfen) absichern. (2) Datums-/Zeitformate in Listen, Diagramm-Achsen und Export-Dateinamen: Anzeige folgt der Sprache, Dateiinhalte und API-Formate bleiben unverändert (invariant). Zu prüfen, ob CSV-/PDF-Spaltenüberschriften schon sprachabhängig sind; Änderung daran ist nicht Teil dieses Changes. (3) Screenreader sprechen Text mit falschem `lang` falsch aus – deshalb `lang`-Attribut und Eigenbezeichnungen der Sprachen.
- **Alternativen:** Sprache am Server speichern (geräteübergreifend, aber unnötige Daten und Netzabhängigkeit beim Start) – verworfen, wie beim Theme. Nur Neustart-Variante im Web (einfacher, aber unnötige Reibung) – nur als Fallback.

## 8. Tasks (Implementierungs-Checkliste)
- [x] T1 (architect) Entscheidung: **Web** – Neurender ohne Neuladen. Zwei Funde: (a) Blazor WebAssembly legt die Kultur des Threads beim Start fest und lädt nur das Sprachpaket der beim Start erkannten Sprache; deshalb eigener `LanguageLocalizer` (liest `ILanguageService.Culture`), Formate über `ILanguageService.Culture` statt `CultureInfo.CurrentCulture`, und beide Sprachpakete werden beim Start geladen (`loadAllSatelliteResources` in `wwwroot/index.html`). (b) `MainLayout` vergibt je Sprachwechsel einen neuen `@key` für Navigation und Seite; die Profilseite ist ausgenommen und formatiert ihre Eingaben selbst neu. **MAUI** – Shell wird neu erzeugt (`AppShell` jetzt transient, alte Shell löst ihre Ereignisse über `Detach()`), danach wieder Profilseite; **Abweichung:** ungespeicherte Profileingaben gehen in der App beim Neuaufbau verloren (nur im Web bleiben sie erhalten).
- [x] T2 (frontend/qa) Tests (Red) aus den Scenarios: Web (bUnit): Auswahl, Speichern/Lesen über die JavaScript-Schnittstelle, Standard bei `fr`/`en`, Zahlenformat, ungespeicherte Eingaben, Statusansage, `lang`; MAUI Core: `LanguageSettings`, `ResxLocalizer` mit wechselbarer Kultur, `ProfileViewModel`, DE/EN-Parität der neuen Schlüssel
- [x] T3 (frontend) Web: `LanguageService`, Start in `Program.cs`, Abschnitt in `Profile.razor`, Layout-Neurender, `lang`-Attribut, Resx DE/EN
- [x] T4 (frontend) MAUI: `LanguageSettings`, `ResxLocalizer`, `ProfileViewModel`, `ProfilePage`, Shell-Neuaufbau, Resx DE/EN, statischer XAML-A11y-Test
- [x] T5 (qa) `Tests/a11y/check.mjs` um die Sprachwahl erweitern (deutsch und englisch, hell/dunkel); Browser-Prüfung gegen die echte API (`check.mjs`: 87 Prüfungen, davon alle Seiten in Englisch bei 1280/375/320 px und der Laufzeit-Wechsel); MAUI auf dem **Android-Emulator** geprüft (Wechsel auf Deutsch, Tab-Titel, Anmeldung bleibt, Neustart behält die Wahl, „System“ folgt dem Gerät). **Nachträglich geprüft (2026-11-05):** iOS-26.5-Simulator (iPhone 17) – Wechsel auf English (Titel, Tabs, Seite, Anmeldung bleibt) und Neustart behält die Wahl; der Build lief mit `-p:ValidateXcodeVersion=false`, weil die iOS-Workload 27.1 Xcode 27.1 verlangt und 27.2 installiert ist. **Nicht geprüft:** Mac Catalyst, Windows; blockierter `localStorage` nur im JavaScript per try/catch umgesetzt, nicht in einem Browser getestet
- [x] T6 (ux/doc) Screenreader-Abläufe in `docs/a11y/screenreader-pruefung.md` und `docs/a11y/maui-screenreader-pruefung.md` ergänzen; README (Profilseite, Sprachwahl)

## 9. Acceptance Criteria (DoD)
- [x] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün (CI). — CI auf `main` grün (PR 59). Nicht automatisiert: „Speicher nicht verfügbar“ (JavaScript) und „Gerätesprache ohne Wahl“ in der App-Oberfläche.
- [~] Sprachwahl wirkt sofort in Web und MAUI, bleibt nach Neuladen/Neustart erhalten und folgt bei „System“ dem Gerät (Browser, Android-Emulator und iOS-Simulator geprüft, siehe T5).
- [x] Alle sichtbaren Texte beider Oberflächen sind in beiden Sprachen vorhanden (DE/EN-Paritätstests grün); keine hartcodierten sichtbaren Texte.
- [x] axe-core (Job `A11y`): keine Verstöße „serious/critical“ in beiden Sprachen und Schemata; statischer XAML-Test grün. — Lokal 87 von 87 und Job `A11y` in der CI grün.
- [x] Keine Sprachangabe wird an den Server gesendet (Test bzw. Review). — Test `SetChoice_NeverSendsAnythingToTheServer` (Dienst kennt nur JavaScript); in der App nur `ISettingsStore`.
- [~] Screenreader-Durchlauf durch eine Person oder ausdrücklich als offen vermerkt (nicht als bestanden ausgeben). — **Offen**; Abläufe 15 (Web) und 17 (App) stehen in den Anleitungen.
- [x] Spec archiviert, Requirements nach `UserStories/specs/profil-einstellungen.openspec.md` übernommen. — Archiviert am 2026-11-05.
