# Change: Profil- und Einstellungsseite (Darstellung, Alter, Größe, Gewicht)

> **Status:** in-progress (umgesetzt; offen: Screenreader-Durchlauf, Android-Prüfung)
> Ablage: `UserStories/profil-einstellungen.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-profil-einstellungen.openspec.md`)
> Baut auf: Identity-Anmeldung (Epic 1), Refit-Clients in `CleanArchitecture.Contracts`, Web-Oberfläche (`UserStories/specs/frontend-oberflaeche.openspec.md`) und MAUI-App (`UserStories/maui-app-api-anbindung.openspec.md`).

## 1. Proposal (Why & What)
- **Why:** Nutzer können die Darstellung nicht wählen (die Oberfläche folgt nur dem Systemschema), und es gibt keinen Ort für persönliche Angaben. Alter, Größe und Gewicht sind Grundlage für spätere Auswertungen und werden hier erstmals erfasst.
- **What Changes:**
  1. Neue Seite **Profil & Einstellungen** in Web und MAUI-App.
  2. **Darstellung:** Auswahl *System*, *Hell*, *Dunkel* (Standard: System). Gilt je Gerät und wird lokal gespeichert, **nicht** am Server.
  3. **Profildaten:** Alter, Größe (cm), Gewicht (kg). Sie werden am Server je Nutzer gespeichert und sind auf allen Geräten gleich. Gespeichert wird das **Geburtsjahr**, angezeigt und eingegeben wird das **Alter** (so bleibt das Alter über die Jahre richtig).
  3. **BMI:** Aus Größe und Gewicht wird der Body-Mass-Index als Zahl berechnet und angezeigt, mit dem Hinweis „keine Diagnose“. **Keine Kategorie/Bewertung** (Untergewicht, Übergewicht …) in dieser Stufe: die Grenzen sind nicht abgesichert und werden bei Bedarf per eigenem Change nach Quellenprüfung ergänzt.
- **Impact:** Neuer Endpunkt `/api/v1/profile`, neue Tabelle `UserProfile` (Migration), Refit-Client, Blazor-Seite (inkl. Umstellung der Farbschemata von `prefers-color-scheme` auf explizit wählbar), MAUI-Seite (inkl. `UserAppTheme`), Tests, Doku. **Nicht** Teil: imperiale Einheiten (Zoll/Pfund), Geschlecht, Konto löschen, Nutzung der Daten für Grenzwerte oder KI (Epic 9), Gewichtsverlauf.

## 2. User Stories
- Als Nutzer möchte ich zwischen hellem, dunklem und dem System-Schema wählen, damit die App für meine Augen und Umgebung passt.
- Als Nutzer möchte ich mein Alter, meine Größe und mein Gewicht eintragen und ändern.
- Als Nutzer möchte ich meinen BMI sehen, ohne dass die App mir eine Bewertung aufdrängt.
- Als Nutzer möchte ich meine Profildaten jederzeit wieder löschen können.

## 3. Requirements (Delta)

### ADDED Requirements

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

## 4. UI/UX Requirements
- **Web:** `Pages/Profile.razor` unter `/profile`, Navigationseintrag „Profil“; Abschnitt „Darstellung“ (Radiogruppe), Abschnitt „Persönliche Angaben“ (Alter, Größe, Gewicht, BMI-Anzeige, Speichern, Löschen). Dienst `ThemeService` (JS-Interop: `localStorage`, `data-theme`); Theme wird beim Start vor dem ersten Rendern gesetzt (kein Aufblitzen im falschen Schema).
- **MAUI:** `ProfilePage` + `ProfileViewModel` (Core), `ThemeService` über `Application.Current.UserAppTheme` und `Preferences`; Erreichbar über einen sechsten Tab oder, wegen der schmalen Tab-Leiste (fünf Tabs passen bereits knapp), über einen Eintrag in der Navigationsleiste neben „Abmelden“ (Entscheidung im Design-Review, Tab-Breite auf Android prüfen).
- **State:** Theme je Gerät lokal; Profil am Server, Formularstand im ViewModel/in der Seite.
- **i18n/a11y:** Schlüsselpräfixe `Profile_`, `Theme_`; Eingabefelder `inputmode="numeric"`/`Keyboard="Numeric"`; Zahlenformat nach Kultur (Komma/Punkt bei Gewicht), Eingabe mit beiden Dezimaltrennern akzeptieren; Zielgröße ≥ 44 pt; statische A11y-Tests (XAML) und axe-core-Lauf müssen die neue Seite einschließen.

## 5. API & Backend Requirements
- **Authentifizierung:** `[Authorize]`, Daten ausschließlich des angemeldeten Nutzers.
- **Endpoints:**
  - `GET /api/v1/profile` → `ProfileDto(int? Age, int? HeightCm, decimal? WeightKg)`; leeres Profil statt 404.
  - `PUT /api/v1/profile` → ersetzt das Profil; Validierung per FluentValidation (Alter 0–120, Größe 50–250, Gewicht 2–500 mit höchstens einer Nachkommastelle) mit `ValidationProblemDetails` (camelCase-Felder `age`, `heightCm`, `weightKg`).
  - `DELETE /api/v1/profile` → 204.
- **Modelle (PostgreSQL):** `UserProfile`: `UserId` (PK, String), `BirthYear` (int?, intern), `HeightCm` (int?), `WeightKg` (numeric(4,1)?), `UpdatedAt`. Neue Migration `AddUserProfile`; kein Eingriff in bestehende Migrationen.
- **Domain/Application:** Aggregat `UserProfile` mit validierenden Setzern; Alter wird aus `TimeProvider` und Geburtsjahr berechnet (testbar mit `FakeTimeProvider`); Wolverine-Handler `GetProfile`, `SaveProfile`, `DeleteProfile`; Repository-Interface in der Domain.
- **Contracts:** `IProfileApi` (Refit) + DTOs, Registrierung in `AddBloodPressureApiClient`.

## 6. KI/AI Requirements
Entfällt. (Profildaten dürfen später nur nach eigener Entscheidung und ohne Personenbezug in Prompts einfließen, siehe Epic 9.)

## 7. Design (Technischer Ansatz)
- **Theme getrennt vom Profil:** Die Darstellung ist keine Gesundheitsangabe und gehört nicht an den Server; lokale Ablage vermeidet Netzwerkabhängigkeit beim Start. Folge: je Gerät eigene Wahl (bewusst).
- **Web-Theme-Umbau (Risiko):** `styles.css` nutzt an vielen Stellen `@media (prefers-color-scheme: …)` sowie das daisyUI/Tailwind-Stylesheet `app.min.css`, das ebenfalls dem Systemschema folgt. Zu klären zu Beginn: unterstützt `app.min.css` ein `data-theme`-Attribut bzw. die Tailwind-`dark`-Strategie per Klasse? Sonst muss die Systemschema-Logik per JS in ein `data-theme` übersetzt werden (Anfangsskript in `index.html`). Dieser Umbau ist der aufwendigste Teil; die axe-core-Läufe (hell/dunkel) sichern ihn ab.
- **MAUI-Theme:** `UserAppTheme = Light/Dark/Unspecified`; die bestehenden `AppThemeBinding`-Ressourcen reagieren automatisch. Das Diagramm (`ChartDrawable`) liest `RequestedTheme` und muss bei Wechsel neu zeichnen (`RequestedThemeChanged` ist schon verdrahtet).
- **Alter als Geburtsjahr:** Eingabe „Alter 42“ im Jahr 2026 → `BirthYear = 1984`; Rundungsfehler von höchstens einem Jahr (Geburtstag unbekannt) sind bekannt und akzeptiert.
- **BMI:** Reine Anzeige-Berechnung im Client (`ProfileCalculator` in der Contracts- bzw. Core-Schicht, mit Tests); Rundung „kaufmännisch“ auf eine Nachkommastelle.
- **Datenschutz:** Gesundheitsdaten nach DSGVO besonders schützenswert. Löschen ist ausdrücklich vorgesehen; Aufbewahrung/Auskunft/Kontolöschung sind **nicht** Teil dieses Changes und als Folge-Epic zu führen. Keine Profildaten in Logs (Strukturiertes Logging ohne Werte).
- **Alternativen:** Theme am Server speichern (geräteübergreifend, aber Netzabhängigkeit und unnötige Daten) – verworfen; Alter als Zahl speichern – verworfen (veraltet).

## 8. Tasks (Implementierungs-Checkliste)
- [x] T1 (architect) Klärung Theme-Umbau Web (`data-theme` vs. Systemschema, Verhalten von `app.min.css`), ADR falls nötig
- [x] T2 (backend) Tests (Red) aus den Scenarios: Domain (Alter/Geburtsjahr, Grenzen), Handler, API (GET/PUT/DELETE, 400/401, Mandantentrennung), Export ohne Profildaten
- [x] T3 (backend) Domain, Application, Infrastruktur, Migration `AddUserProfile`, Controller, OpenAPI-Beschreibung
- [x] T4 (backend/frontend) `IProfileApi` (Refit) und DTOs
- [x] T5 (frontend) Web: `ThemeService`, Theme-Umbau `styles.css`, `Profile.razor`, Navigation, bUnit-Tests (inkl. `localStorage` blockiert, DE/EN-Parität)
- [x] T6 (frontend) MAUI: `ThemeService`, `ProfileViewModel` + Core-Tests, `ProfilePage`, Navigation, statischer XAML-A11y-Test grün
- [x] T7 (qa) axe-core-Lauf (`Tests/a11y/check.mjs`) um die neue Seite erweitern; hell/dunkel × 3 Breiten; Browser-Prüfung gegen die echte API
- [x] T8 (qa) MAUI geprüft gegen die lokale API: iOS 26.5 Simulator und Android 35 Emulator – Seite, Wechsel auf Dunkel sofort (iOS zusätzlich nach Kaltstart), Eingabe, BMI live, Speichern (Server liefert `{"age":42,"heightCm":180,"weightKg":82.5}`), Löschen mit Bestätigung (Abbrechen ändert nichts, „Löschen“ leert das Profil auf dem Server), Diagramm im dunklen Modus lesbar. **Nicht geprüft:** Windows-Build, Screenreader (T9). Beobachtung (kein Fehler): Beim erneuten Öffnen der Seite bleibt die letzte Statusmeldung („Das Profil wurde gespeichert.“) stehen.
- [x] T8b (qa) Migration auf einer Datenbank mit Bestandsdaten: `Tests/CleanArchitecture.Tests/MigrationDataTests.cs` (Kategorien → `Hypertension`, Hinweistext, Rollback auf `Grade3`, doppelte Migration)
- [~] T9 (ux) Review: Tastatur, Fokus, Ansagen, Zielgrößen; Screenreader-Abläufe in beide Anleitungen (`docs/a11y/`) ergänzen
- [x] T10 (doc) README (neue Seite, Theme-Hinweis), Specs aktualisieren

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün (CI).
- [ ] Migration läuft auf leerer und auf bestehender Datenbank; bestehende Daten bleiben unverändert.
- [ ] Theme-Wahl wirkt sofort, bleibt nach Neustart erhalten und folgt bei „System“ dem Gerät (Web und MAUI im echten Browser/Simulator geprüft); kein Aufblitzen im falschen Schema beim Laden.
- [ ] axe-core: keine Verstöße „serious/critical“ auf der neuen Seite in beiden Schemata (Web); statischer XAML-Test grün (MAUI).
- [ ] Keine Profilangaben in Exporten, Logs oder Hinweisen (Test bzw. Review).
- [ ] BMI ohne Kategorie, mit Hinweis „keine Diagnose“.
- [ ] Screenreader-Durchlauf durch eine Person oder ausdrücklich als offen vermerkt (nicht als bestanden ausgeben).
- [ ] Spec archiviert, Requirements nach `UserStories/specs/profil-einstellungen.openspec.md` übernommen.
