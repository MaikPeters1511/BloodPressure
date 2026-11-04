# Change: Blazor-UI an die abgesicherte API anpassen

> **Status:** in-progress
> Ablage: `UserStories/frontend-blazor-api-anpassung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-frontend-blazor-api-anpassung.openspec.md`)

## 1. Proposal (Why & What)
- **Why:** Die Blazor-UI passt nicht mehr zur API: Sie baut nicht (.NET 8 gegen .NET-10-Projekte), ruft die API ohne Token auf (jetzt 401), liest das alte Listenformat und nutzt eine hartcodierte URL (`https://localhost:5000`) mit Newtonsoft. Texte sind hartcodiert (gegen die i18n-Vorgabe).
- **What Changes:**
  - UI auf .NET 10, Zugriff über den typisierten Refit-Client (`IBloodPressureApi`) aus `CleanArchitecture.Contracts`; Newtonsoft entfällt.
  - Anmeldung und Registrierung (Seite „Login“), Token im Browser (`sessionStorage`), automatische Erneuerung über das Refresh-Token, `IAccessTokenProvider` für den Refit-Client.
  - Seite „Messwerte“ ersetzt „API“: paginierte Liste (neueste zuerst) mit Kategorie, Messwert anlegen, Messwert löschen (mit `If-Match`).
  - API-Adresse aus `wwwroot/appsettings.json` (`Api:BaseUrl`).
  - i18n DE/EN (`IStringLocalizer`, `.resx`), Sprache aus dem Browser; Barrierefreiheit (Labels, `role="alert"`, Tabellen-Caption, Tastaturbedienung).
  - `IAuthApi` (Registrierung, Login, Refresh) in `Contracts`, damit auch MAUI es nutzen kann.
- **Impact:** `CleanArchitecture.Presentation.UI`, `CleanArchitecture.Contracts`, neues Testprojekt `Tests/CleanArchitecture.UI.Tests` (bUnit), CI. **Nicht** Teil: Statistik, Export, Erinnerungen/Benachrichtigungen, Erinnerungen in der UI, MAUI, Hosting der UI in .NET Aspire.

## 2. User Stories
- Als Nutzer möchte ich mich anmelden, damit ich meine Messwerte sehe.
- Als Nutzer möchte ich meine Messwerte seitenweise ansehen und ihre Einordnung erkennen.
- Als Nutzer möchte ich einen Messwert eintragen und löschen können und verständliche Fehlermeldungen sehen.
- Als Nutzer möchte ich die App auf Deutsch oder Englisch und mit Tastatur/Screenreader bedienen können.

## 3. Requirements (Delta)

### ADDED Requirements

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

### MODIFIED Requirements
**API-Zugriff der UI:** Die UI SHALL die API ausschließlich über `IBloodPressureApi`/`IAuthApi` mit der Basisadresse aus der Konfiguration (`Api:BaseUrl`) ansprechen; hartcodierte URLs und Newtonsoft entfallen.

### REMOVED Requirements
Die Seite „API“ (`Api.razor`) entfällt; ihre Funktion übernimmt „Messwerte“.

## 4. UI/UX Requirements
- **Komponenten:** `Login.razor`, `Measurements.razor`, angepasstes `NavMenu.razor` (Messwerte, Anmelden/Abmelden), `Home.razor` (Willkommen mit Link).
- **State:** `AuthSession` (Tokens, angemeldet ja/nein), Liste mit aktueller Seite, Formularzustand.
- **i18n/a11y:** siehe Requirement oben; Ressourcen `SharedResource.resx` und `SharedResource.en.resx` im Projektstamm.

## 5. API & Backend Requirements
- Neue Contracts: `IAuthApi` (`POST /api/auth/register`, `/login`, `/refresh`), `CredentialsRequest`, `RefreshRequest`, `TokenResponse`. Die Backend-API bleibt unverändert; `IAuthApi` trägt bewusst keinen Token-Handler (kein Zirkelbezug mit der Tokenerneuerung).

## 6. Design (Technischer Ansatz)
- UI auf `net10.0`; Referenz auf `Contracts` statt `Application`.
- Tokens in `sessionStorage` statt `localStorage`: sie verfallen mit dem Tab; sie sind trotzdem per XSS auslesbar. Ein BFF/Cookie-Ansatz wäre sicherer, ist aber eine größere Architekturänderung.
- Kulturwahl über eine kleine JS-Funktion (`navigator.language`), Standard Deutsch.
- Tests mit bUnit und handgeschriebenen Fakes für `IBloodPressureApi`/`IAuthApi`/Tokenspeicher.

## 7. Tasks
- [x] 1. bUnit-Tests aus den Scenarios schreiben (Red) – `Tests/CleanArchitecture.UI.Tests`
- [x] 2. `IAuthApi` in Contracts
- [x] 3. UI-Projekt auf .NET 10, `AuthSession`, Speicher, Konfiguration, Lokalisierung
- [x] 4. Seiten `Login` und `Measurements`, Navigation
- [x] 5. CI um UI-Build und -Tests erweitern
- [x] 6. Manuelle Prüfung im Browser gegen die laufende API (Registrierung, Liste, Anlegen, ungültige Eingabe, Löschen mit Bestätigung); eine fachliche UX-/Screenreader-Prüfung durch den `ux-agent` steht noch aus

## 8. Acceptance Criteria (DoD)
- [x] Jedes Scenario ist durch einen bUnit-Test abgedeckt und grün (32 Tests).
- [x] UI baut und startet gegen die laufende API (Login, Liste, Anlegen, Löschen), im Browser geprüft.
- [x] Keine hartcodierten sichtbaren Texte in den neuen Seiten; keine Newtonsoft-Abhängigkeit. (Die Vorlagenseiten Counter und Weather sind unverändert und nicht mehr verlinkt.)
- [ ] Spec archiviert.
