# Change: MAUI-App – Anmeldung, Messwerte, Auswertung, Export, Erinnerungen, Hinweise

> **Status:** in-progress (Phase 1 bis 4 umgesetzt; offen: Screenreader-Durchlauf auf dem Gerät)
> Ablage: `UserStories/maui-app-api-anbindung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-maui-app-api-anbindung.openspec.md`)
> Baut auf: `UserStories/specs/frontend-oberflaeche.openspec.md` (gleiche Fähigkeiten wie die Blazor-Oberfläche), Refit-Clients in `CleanArchitecture.Contracts`.

## 0. Ausgangslage (Korrektur)
Die MAUI-App ist **noch die unveränderte Projektvorlage** („Hello, World!“, Zähler-Schaltfläche). Sie hat weder Anmeldung noch Messwerte noch einen API-Client. Frühere Aussagen, die App habe „Login und Messwerte“, waren falsch; nur die Zielframeworks wurden auf .NET 10 angehoben. Dieser Change baut die App erstmals fachlich auf.

## 1. Proposal (Why & What)
- **Why:** Blutdruckwerte werden meist mobil erfasst. Die Blazor-Oberfläche deckt alle Funktionen ab, die MAUI-App nichts.
- **What Changes:** MAUI-App mit Anmeldung, Messwerte (Liste, Anlegen, Löschen), Auswertung, Export (Teilen/Speichern), Erinnerungen und Hinweisen. Gleiche API (`/api/v1`), gleiche Refit-Clients, DE/EN, Barrierefreiheit.
- **Impact:** Neue Klassenbibliothek `CleanArchitecture.Presentation.Maui.Core` (net10.0: ViewModels, Services, Texte – ohne MAUI-Abhängigkeit, damit in der CI testbar), `CleanArchitecture.Presentation.Maui` (Views, DI, plattformspezifische Dienste), `Tests/`. **Kein** API-Change. **Nicht** Teil: Push-Benachrichtigungen/lokale Alarme auf dem Gerät (Erinnerungen werden serverseitig verwaltet und nur eingestellt), Geräteimport (Epic 10), KI (Epic 9), Windows-Zielplattform-Abnahme.

## 2. User Stories
- Als Nutzer möchte ich mich in der App anmelden und angemeldet bleiben, damit ich meine Werte unterwegs erfasse.
- Als Nutzer möchte ich Messungen anlegen, ansehen und löschen, damit mein Verlauf aktuell ist.
- Als Nutzer möchte ich Auswertungen, Export, Erinnerungen und Hinweise wie im Web nutzen.
- Als Nutzer mit Screenreader (TalkBack/VoiceOver) möchte ich alle Funktionen bedienen können.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Anmeldung und sichere Token-Ablage
Das System SHALL Anmeldung und Registrierung über `IAuthApi` anbieten, Tokens ausschließlich im plattformeigenen sicheren Speicher (`SecureStorage`: Keychain/Keystore) ablegen, abgelaufene Access-Token per Refresh-Token erneuern und bei fehlgeschlagener Erneuerung auf die Anmeldung zurückführen.

##### Scenario: Erfolgreiche Anmeldung
- **GIVEN** gültige Zugangsdaten
- **WHEN** der Nutzer „Anmelden“ wählt
- **THEN** werden Tokens sicher gespeichert und die Messwerte-Seite erscheint

##### Scenario: Falsche Zugangsdaten
- **GIVEN** falsches Passwort
- **WHEN** der Nutzer anmeldet
- **THEN** zeigt die App eine lokalisierte Fehlermeldung ohne technische Details und speichert nichts

##### Scenario: App-Neustart
- **GIVEN** gespeicherte Tokens
- **WHEN** die App startet
- **THEN** ist der Nutzer angemeldet (nach Bedarf mit Token-Erneuerung)

##### Scenario: Abmelden
- **GIVEN** angemeldeter Nutzer
- **WHEN** er sich abmeldet
- **THEN** sind alle gespeicherten Tokens gelöscht und die Anmeldeseite erscheint

#### Requirement: Messwerte
Das System SHALL Messungen paginiert (neueste zuerst) mit Kategorie als Text und Farbmarkierung anzeigen, das Anlegen mit clientseitiger und serverseitiger Validierung (Feldmeldungen aus `ValidationProblemDetails`) und das Löschen mit Bestätigung und `If-Match` (ETag) erlauben; bei 401 wird zur Anmeldung zurückgeleitet, bei 412/409 erscheint ein Konflikthinweis.

##### Scenario: Liste und Nachladen
- **GIVEN** mehr als eine Seite Messungen
- **WHEN** der Nutzer ans Listenende scrollt
- **THEN** wird die nächste Seite nachgeladen

##### Scenario: Validierungsfehler
- **GIVEN** Systolisch 999
- **WHEN** gespeichert wird
- **THEN** erscheint die Feldmeldung der API am Feld und der Fokus bzw. die Screenreader-Ansage geht auf den Fehler

##### Scenario: Löschen mit Konflikt
- **GIVEN** eine zwischenzeitlich geänderte Messung
- **WHEN** der Nutzer löscht
- **THEN** zeigt die App den Konflikthinweis und lädt die Liste neu

#### Requirement: Auswertung
Das System SHALL Kennzahlen und Verlauf (Durchschnitt Systolisch/Diastolisch/Puls) je Tag, Woche, Monat für einen Zeitraum anzeigen; Diagramm mit zusätzlicher Wertetabelle als Alternative für Screenreader.

##### Scenario: Intervall wechseln
- **GIVEN** die Auswertung
- **WHEN** der Nutzer „Woche“ wählt
- **THEN** wird `interval=week` angefragt und die Anzeige aktualisiert

##### Scenario: Keine Daten
- **GIVEN** keine Messung im Zeitraum
- **WHEN** die Seite lädt
- **THEN** erscheint ein erklärender Leerzustand

#### Requirement: Export
Das System SHALL CSV oder PDF für einen Zeitraum laden und über den Teilen-Dialog des Betriebssystems bzw. in einen vom Nutzer gewählten Speicherort ausgeben; bei 400 (zu viele Messungen) erscheint eine lokalisierte Meldung.

##### Scenario: PDF teilen
- **GIVEN** Format PDF
- **WHEN** der Nutzer „Exportieren“ wählt
- **THEN** wird die Datei temporär gespeichert und der Teilen-Dialog geöffnet; die Temp-Datei wird danach entfernt

#### Requirement: Erinnerungen und Hinweise
Das System SHALL Erinnerungen (Zeitzone, bis zu 5 Uhrzeiten) bearbeiten und speichern, Hinweise anzeigen, als gelesen markieren und einen Ungelesen-Zähler am Tab/Menü zeigen. Warnhinweise enthalten den Hinweis „keine ärztliche Beratung“.

##### Scenario: Zeitzone vorschlagen
- **GIVEN** keine gespeicherten Erinnerungen
- **WHEN** die Seite öffnet
- **THEN** ist die Zeitzone des Geräts (`TimeZoneInfo.Local.Id`) vorbelegt

##### Scenario: Hinweis als gelesen markieren
- **GIVEN** ein ungelesener Hinweis
- **WHEN** der Nutzer ihn markiert
- **THEN** wird die API aufgerufen, der Zähler sinkt und die Änderung wird per `SemanticScreenReader.Announce` angesagt

#### Requirement: Konfigurierbare API-Adresse
Das System SHALL die API-Basisadresse aus der Konfiguration beziehen (nie fest im Code), je Build-Konfiguration überschreibbar (Debug: lokale Adresse, Android-Emulator `10.0.2.2`), Klartext-HTTP nur in Debug-Builds erlauben.

##### Scenario: Release-Build
- **GIVEN** ein Release-Build
- **WHEN** die App eine `http://`-Adresse konfiguriert bekäme
- **THEN** wird der Start mit einer klaren Konfigurationsfehlermeldung abgebrochen statt Klartext zu nutzen

#### Requirement: i18n und Barrierefreiheit
Das System SHALL alle sichtbaren Texte über `.resx` (DE/EN, Gerätesprache) ausgeben, jedem Bedienelement `SemanticProperties` (Description/Hint, Überschriften-Ebenen) geben, Mindestzielgröße 44 × 44 pt einhalten, Farben nie als einzigen Informationsträger nutzen und Dynamic Type/Schriftvergrößerung ohne Abschneiden unterstützen.

##### Scenario: Englisches Gerät
- **GIVEN** Gerätesprache Englisch
- **WHEN** eine Seite erscheint
- **THEN** sind alle Texte englisch; ein Test prüft DE/EN-Schlüsselparität

### MODIFIED Requirements
_Entfällt._ (Die Vorlageninhalte `MainPage` mit Zähler werden ersetzt.)

### REMOVED Requirements
- `MainPage` („Hello, World!“, Zähler): Vorlage ohne Funktion, wird entfernt.

## 4. UI/UX Requirements
- **Aufbau:** Shell mit Tabs: Messwerte, Auswertung, Export, Erinnerungen, Hinweise (+ Abmelden); Anmeldung als eigene Route außerhalb der Tabs.
- **Views (XAML)/ViewModels (Core):** `LoginPage/LoginViewModel`, `MeasurementsPage/MeasurementsViewModel`, `StatisticsPage/StatisticsViewModel` (Diagramm per `GraphicsView` + Tabelle), `ExportPage/ExportViewModel`, `RemindersPage/RemindersViewModel`, `NotificationsPage/NotificationsViewModel`.
- **State:** je ViewModel; global nur Sitzung (`ISessionService`) und Ungelesen-Zähler.
- **Plattform:** Tastatur-/Fokusreihenfolge, `AutomationId` für UI-Tests, Hell/Dunkel über `AppThemeBinding`.

## 5. API & Backend Requirements
- Keine neuen Endpunkte; genutzt werden alle Refit-Clients (`IAuthApi`, `IBloodPressureApi`, `IReminderApi`, `INotificationApi`) mit `BearerTokenHandler` und einem `IAccessTokenProvider` auf Basis von `SecureStorage`.
- **CORS:** gilt nur für Browser; native Clients sind nicht betroffen.
- **TLS:** Produktion nur HTTPS; keine Zertifikatsprüfung abschalten.

## 6. KI/AI Requirements
Entfällt.

## 7. Design (Technischer Ansatz)
- **Trennung für Testbarkeit:** Logik in `…Maui.Core` (net10.0, ohne MAUI) – ViewModels, Validierung, Formatierung, Sitzung (`ISecureStore`-Abstraktion), `IShareService`, `IDeviceInfo`. Die MAUI-App stellt nur Implementierungen (SecureStorage, Share) und Views. So laufen ViewModel-Tests auf Linux in der bestehenden CI; MAUI-Workloads sind im CI-Runner nicht nötig.
- **MVVM-Bibliothek:** `CommunityToolkit.Mvvm` (Source-Generatoren für `ObservableProperty`/`RelayCommand`) – eine neue Abhängigkeit, vor Umsetzung per Zustimmung/ADR bestätigen; Alternative: eigenes minimales `ObservableObject`.
- **Texte:** `.resx` in Core, `LocalizedStrings`-Wrapper für XAML; Quelle der Wahrheit für Schlüssel analog `SharedResource` der Web-UI, Wiederverwendung gemeinsamer Texte erwägen (Folge-Refactoring, nicht Teil dieses Changes).
- **Diagramm:** `GraphicsView` mit `IDrawable`; Koordinatenberechnung wiederverwenden (`ChartGeometry` aus der Web-UI in eine gemeinsame Bibliothek heben – Entscheidung im Design-Review, sonst Kopie mit Test).
- **Phasen:** (1) Core + Anmeldung + Messwerte, (2) Auswertung + Export, (3) Erinnerungen + Hinweise, (4) A11y-Abnahme. Jede Phase eigener PR.
- **Risiken:** Keine physischen Geräte/Emulatoren in der CI; Abnahme der Views nur manuell (Simulator). MAUI-Build-Voraussetzungen (Xcode-Version) wurden früher mit `ValidateXcodeVersion=false` umgangen; Android-Build/Windows-Build bisher ungeprüft.

## 8. Tasks (Implementierungs-Checkliste)
- [x] T1 (architect) Entscheidung MVVM-Bibliothek und Core-Aufteilung als ADR
- [x] T2 (frontend) Phase 1 Tests (Red): Sitzung/Token, LoginViewModel, MeasurementsViewModel (Validierung, Paging, Löschen, 401/409)
- [x] T3 (frontend) Core-Projekt, `SecureStorage`-Adapter, DI, Konfiguration der API-Adresse, Login- und Messwerte-Views, Shell
- [x] T4 (frontend) Phase 2: Auswertung (GraphicsView + Tabelle), Export (Teilen)
- [x] T5 (frontend) Phase 3: Erinnerungen, Hinweise, Zähler
- [ ] T6 (qa) Lauf im iOS-Simulator (und Mac Catalyst) gegen die echte API, Screenshots als Nachweis
- [x] T7 (ux) A11y-Review: statischer XAML-Test in der CI, Gegenprobe, Accessibility-Baum der Mac-App (Login), Geräte-Anleitung `docs/a11y/maui-screenreader-pruefung.md`. **Offen: Durchlauf mit VoiceOver/TalkBack durch eine Person**
- [ ] T8 (devops) CI: ViewModel-Tests laufen bereits über `dotnet test`; optional macOS-Job für `dotnet build` des MAUI-Projekts (Kosten/Nutzen abwägen)
- [x] T9 (doc, Stand Phase 1) README: Start der App, API-Adresse, Emulator-Hinweise

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3, das Logik betrifft, ist durch einen Core-Test abgedeckt und grün (CI).
- [ ] Views im Simulator manuell durchgespielt (Anmelden → Messung anlegen → Auswertung → Export → Erinnerung → Hinweis) mit Nachweis.
- [ ] Keine Tokens/Passwörter im Klartext gespeichert oder geloggt; Release-Build lehnt `http://` ab.
- [ ] DE/EN-Paritätstest grün; keine hartcodierten sichtbaren Texte.
- [ ] Screenreader-Prüfung (VoiceOver/TalkBack) durch eine Person protokolliert, oder offen vermerkt (nicht als bestanden ausgeben).
- [x] Android-Build und -Lauf geprüft (Emulator API 35: Anmeldung, Messwerte, Auswertung mit Diagramm, Erinnerungen, Hinweise); Windows-Build ausdrücklich ungeprüft. Zwei Befunde (deutsche Hinweistexte vom Server, abgeschnittener erster Tab-Titel) wurden behoben und auf dem Emulator bestätigt.
- [ ] Spec archiviert, Requirements nach `UserStories/specs/maui-app.openspec.md` übernommen.
