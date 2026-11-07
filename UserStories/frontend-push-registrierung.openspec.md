# Change: Push-Registrierung im Frontend (MAUI iOS/Android und Web)

> **Status:** proposed
> Ablage: `UserStories/frontend-push-registrierung.openspec.md` (nach Abschluss: `UserStories/archive/YYYY-MM-DD-frontend-push-registrierung.openspec.md`)
> Quelle: Epic 6, Task T6.5 (`UserStories/epics/epic-06-erinnerungen-warnungen.openspec.md`), ADR 0003 („Push-Zustellung (FCM/APNs) ist eine Folge-Change“).
> Abhängigkeiten: Epic 1 (Auth), Epic 6 T6.1–T6.4 (gespeicherte Benachrichtigungen, abgeschlossen).

## 0. Bestandsaufnahme (Explore)

**Bereits vorhanden, hier NICHT erneut spezifiziert** (siehe `specs/frontend-oberflaeche.openspec.md`, `specs/maui-app.openspec.md`):
- Web: `Pages/Notifications.razor` und `Pages/Reminders.razor` (Liste neueste zuerst, als gelesen markieren, Ungelesen-Zähler in der Navigation, Warnkennzeichnung `CriticalReading`, Haftungshinweis, Erinnerungen bearbeiten).
- MAUI: `NotificationsPage`, `RemindersPage` mit ViewModels und `NotificationCounter`; Abmelden über `SessionService.LogoutAsync` (löscht Tokens im `SecureStorage`).
- Backend: `GET /api/v1/notifications`, `POST /api/v1/notifications/{id}/read`, `GET/PUT /api/v1/reminders`, `ReminderWorker`. `NotificationDto(Id, Type, Category, Message, CreatedAt, IsRead)` enthält keine Messwerte.

**Fehlt (Gegenstand dieser Spec):**
- Backend: kein Endpunkt, keine Entität/Migration für Geräte-Token; keine Zustellung über FCM/APNs; `INotificationApi` kennt nur Liste und „gelesen“.
- MAUI: keine Push-Pakete/Konfiguration (`.csproj` nur `Microsoft.Maui.Controls` u. a.), kein `POST_NOTIFICATIONS` in `AndroidManifest.xml`, keine Push-Capability/Entitlements für iOS, keine Berechtigungsabfrage, keine Token-Registrierung.
- Web: kein Service Worker, kein Web-Push (`wwwroot` enthält nur `index.html`, CSS, Icons).
- Abmelden entfernt kein Geräte-Token am Server (existiert noch nicht).

## 1. Proposal (Why & What)
- **Why:** Hinweise (kritischer Wert, Messerinnerung) erreichen Nutzer heute nur, wenn die App geöffnet ist. Erinnerungen und Warnungen sind erst mit Zustellung aufs Gerät wirksam.
- **What Changes:**
  - MAUI (iOS, Android): freiwillige Einwilligung mit Erklärtext, Betriebssystem-Berechtigung, Registrierung des Geräte-Tokens am Backend, Aktualisierung bei Token-Wechsel, Entfernen beim Abmelden bzw. Widerruf, Schalter in „Profil & Einstellungen“.
  - Backend (neu): Geräte-Token verwalten und Hinweise als Push zustellen (FCM/APNs).
  - Web (Blazor WASM): Service Worker, Push-API mit VAPID, Einwilligung, Subscription-Registrierung und -Entfernung (Entscheidung zu Frage 2).
  - Backend: höchstens 5 Geräte/Subscriptions je Nutzer, Bereinigung abgelaufener Registrierungen, Löschung bei Konto-Löschen, persistente Zustellung (Outbox mit Zustellstatus) für kritische Hinweise.
  - Nur iOS und Android werden unterstützt; Mac Catalyst und Windows nicht (Entscheidung zu Frage 9).
- **Impact:** Contracts (neue DTOs, `IDeviceApi`), Domain/Application/Infrastructure/API (Geräte-Token, Migration, Push-Versand), MAUI.Core (Service, ViewModel), MAUI (Plattformcode, Manifest/Entitlements), Ressourcen DE/EN, `docs/adr/` (neuer ADR für Push-Anbieter und Datenfluss). Kein Breaking Change an bestehenden Endpunkten.

## 2. User Stories
- Als Nutzer der App möchte ich entscheiden, ob ich Push-Hinweise erhalte, und verstehen, wofür sie genutzt werden, damit ich bewusst zustimme.
- Als Nutzer möchte ich bei einem kritischen Wert oder einer fälligen Erinnerung auch bei geschlossener App benachrichtigt werden, damit ich rechtzeitig reagiere.
- Als Nutzer möchte ich Push jederzeit abschalten und beim Abmelden sicher sein, dass mein Gerät keine Hinweise mehr erhält, damit meine Daten geschützt bleiben.
- Als Nutzer mit Screenreader möchte ich Berechtigungsabfrage, Status und Fehler vollständig bedienen und angesagt bekommen.

## 3. Requirements (Delta)

### ADDED Requirements

#### Requirement: Einwilligung vor Berechtigungsabfrage (MAUI)
Das System SHALL Push nur nach ausdrücklicher, freiwilliger Entscheidung des Nutzers aktivieren. Vor der Betriebssystem-Berechtigungsabfrage SHALL ein eigener Erklärtext (Zweck: Hinweise zu Messerinnerungen und kritischen Werten; Inhalt der Push-Nachricht ohne Messwerte; jederzeit widerrufbar; keine ärztliche Beratung) mit den Aktionen „Aktivieren“ und „Nicht jetzt“ erscheinen. Die Abfrage SHALL NOT automatisch beim App-Start oder Login erfolgen.

##### Scenario: Zustimmen
- **GIVEN** angemeldeter Nutzer, Push noch nicht aktiviert
- **WHEN** er in den Einstellungen „Push-Hinweise aktivieren“ wählt und im Erklärtext „Aktivieren“
- **THEN** erscheint die Betriebssystem-Berechtigungsabfrage

##### Scenario: Nicht jetzt
- **GIVEN** der Erklärtext ist sichtbar
- **WHEN** der Nutzer „Nicht jetzt“ wählt
- **THEN** wird weder die Betriebssystem-Abfrage ausgelöst noch ein Token registriert, und die App bleibt vollständig nutzbar

##### Scenario: Keine automatische Abfrage
- **GIVEN** ein frischer Login
- **WHEN** die Messwerte-Seite erscheint
- **THEN** wurde keine Berechtigungsabfrage ausgelöst

#### Requirement: Berechtigung und Status (MAUI)
Das System SHALL den Berechtigungsstatus (erteilt, abgelehnt, nicht gefragt) ermitteln und in „Profil & Einstellungen“ als Text anzeigen. Bei „abgelehnt“ SHALL ein Hinweis erscheinen, wie die Berechtigung in den Systemeinstellungen geändert wird, mit einer Aktion, die die Systemeinstellungen der App öffnet. Android SHALL die Laufzeitberechtigung `POST_NOTIFICATIONS` (ab Android 13) anfordern; iOS SHALL die Remote-Notification-Berechtigung anfordern.

##### Scenario: Berechtigung erteilt
- **GIVEN** der Nutzer bestätigt die Betriebssystem-Abfrage
- **WHEN** das Gerät ein Token liefert
- **THEN** zeigt die Seite „Push-Hinweise aktiv“ und das Token wird registriert (siehe nächste Requirement)

##### Scenario: Berechtigung abgelehnt
- **GIVEN** der Nutzer lehnt die Betriebssystem-Abfrage ab
- **WHEN** das Ergebnis vorliegt
- **THEN** wird kein Token gesendet, der Status lautet „Push-Hinweise sind in den Systemeinstellungen deaktiviert“ und eine Aktion „Systemeinstellungen öffnen“ ist verfügbar

##### Scenario: Entzug in den Systemeinstellungen
- **GIVEN** Push war aktiv und der Nutzer entzieht die Berechtigung im Betriebssystem
- **WHEN** die App die Einstellungsseite öffnet bzw. in den Vordergrund kommt
- **THEN** zeigt sie den Status „deaktiviert“ und entfernt das Token am Server (siehe Requirement „Token entfernen“)

#### Requirement: Geräte-Token registrieren (MAUI-Client)
Das System SHALL nach erteilter Berechtigung das vom Betriebssystem bzw. Push-Anbieter gelieferte Geräte-Token zusammen mit Plattform (`ios`/`android`) und Kultur (`de`/`en`) an das Backend senden (idempotent), das Token bei jedem Wechsel durch den Anbieter erneut senden und es ausschließlich über den Refit-Client mit Access-Token übertragen. Das Token SHALL NOT geloggt, in Fehlermeldungen oder Analytics ausgegeben werden.

##### Scenario: Erstregistrierung
- **GIVEN** Berechtigung erteilt, Token `T1`
- **WHEN** der Anbieter `T1` liefert
- **THEN** ruft die App einmal `PUT /api/v1/devices/{installationId}` mit `T1`, Plattform und Kultur auf

##### Scenario: Token-Wechsel
- **GIVEN** `T1` ist registriert
- **WHEN** der Anbieter `T2` liefert
- **THEN** wird `T2` für dieselbe Installation gesendet und ersetzt `T1`

##### Scenario: Backend nicht erreichbar
- **GIVEN** die Registrierung scheitert (Netzfehler, 5xx)
- **WHEN** der Aufruf fehlschlägt
- **THEN** zeigt die App eine lokalisierte Meldung ohne technische Details, merkt den Wunsch „aktiv“ und wiederholt die Registrierung beim nächsten App-Start

##### Scenario: Token läuft ab (401)
- **GIVEN** das Access-Token ist abgelaufen
- **WHEN** die Registrierung gesendet wird
- **THEN** greift die bestehende Token-Erneuerung; scheitert sie, führt die App zur Anmeldung

#### Requirement: Token entfernen (Abmelden, Widerruf, Entzug)
Das System SHALL das Geräte-Token am Server löschen (`DELETE /api/v1/devices/{installationId}`), wenn der Nutzer Push ausschaltet, sich abmeldet oder die Berechtigung entzogen ist, und es lokal verwerfen. Das Löschen SHALL vor dem Löschen der Anmelde-Tokens erfolgen. Schlägt es fehl, SHALL die Abmeldung dennoch abgeschlossen werden, die Löschung lokal vorgemerkt und beim nächsten Login derselben oder einer anderen Person auf diesem Gerät nachgeholt werden; zusätzlich SHALL der Server das Token dem sich neu registrierenden Nutzer zuordnen (Umzuordnung) und Registrierungen bereinigen (siehe „Geräte-Token am Backend verwalten“). Das gilt auch, wenn die Anmeldung durch fehlgeschlagene Token-Erneuerung endet (dort ist kein authentifizierter DELETE möglich).

##### Scenario: Push ausschalten
- **GIVEN** Push aktiv
- **WHEN** der Nutzer den Schalter ausschaltet
- **THEN** wird das Token am Server gelöscht, der Status lautet „Push-Hinweise sind aus“, und ein Screenreader-Ansage bestätigt es

##### Scenario: Abmelden
- **GIVEN** Push aktiv
- **WHEN** der Nutzer sich abmeldet
- **THEN** wird zuerst `DELETE …/devices/{installationId}` aufgerufen, danach werden die Anmelde-Tokens gelöscht

##### Scenario: Löschen scheitert beim Abmelden
- **GIVEN** das Netz ist nicht verfügbar
- **WHEN** der Nutzer sich abmeldet
- **THEN** ist er abgemeldet, die Löschung ist lokal vorgemerkt und wird bei der nächsten möglichen Anfrage nachgeholt

##### Scenario: Gerätewechsel des Kontos
- **GIVEN** Nutzer A hat sich abgemeldet, das Löschen war nicht möglich
- **WHEN** Nutzer B meldet sich auf demselben Gerät an und aktiviert Push
- **THEN** ist das Token danach ausschließlich Nutzer B zugeordnet und Nutzer A erhält keine Push-Nachrichten mehr an dieses Gerät

#### Requirement: Geräte-Token und Web-Subscriptions am Backend verwalten
Das System SHALL `PUT /api/v1/devices/{installationId}` (legt an oder aktualisiert) und `DELETE /api/v1/devices/{installationId}` bereitstellen, beide mit `[Authorize]` und nur für den angemeldeten Nutzer. Der Body von `PUT` SHALL Plattform (`ios`, `android`, `web`) und Kultur (`de`/`en`) enthalten; für `ios`/`android` das Geräte-Token, für `web` die Push-Subscription (`endpoint`, `keys.p256dh`, `keys.auth`, optional `expirationTime`). Ein Token bzw. Subscription-Endpoint SHALL höchstens einem Nutzer zugeordnet sein; registriert ein anderer Nutzer dasselbe Token/Endpoint, wird es ihm umgeordnet und beim bisherigen Nutzer entfernt. Pro Nutzer SHALL es höchstens 5 Registrierungen (Geräte inkl. Web-Subscriptions) geben. Ungültige Eingaben SHALL als `ValidationProblemDetails` (400) abgelehnt werden. Tokens und Subscription-Daten SHALL NOT in Logs, Fehlerantworten, Exporten oder Hinweistexten erscheinen. Registrierungen, die seit 60 Tagen nicht aktualisiert wurden, SHALL serverseitig gelöscht werden (Frist: konfigurierbar); der Client SHALL daher bei App-Start bzw. Seitenaufruf mit aktivem Push die Registrierung erneuern.

##### Scenario: Sechstes Gerät
- **GIVEN** ein Nutzer hat 5 Registrierungen
- **WHEN** er eine sechste mit neuer `installationId` registriert
- **THEN** lehnt die API ab (Vorschlag: 409 `problem+json`; Verhalten siehe Offene Frage 12) und die fünf bestehenden bleiben unverändert

##### Scenario: Bestehende Registrierung aktualisieren bei 5 Geräten
- **GIVEN** ein Nutzer hat 5 Registrierungen
- **WHEN** er für eine vorhandene `installationId` erneut `PUT` aufruft
- **THEN** antwortet die API mit 204

##### Scenario: Web-Subscription
- **GIVEN** ein angemeldeter Nutzer
- **WHEN** er `PUT …/devices/{id}` mit Plattform `web`, `endpoint`, `p256dh` und `auth` aufruft
- **THEN** antwortet die API mit 204; fehlt ein Schlüssel, mit 400 und Feldmeldung

##### Scenario: Umordnung
- **GIVEN** Token `T` ist Nutzer A zugeordnet
- **WHEN** Nutzer B `T` registriert
- **THEN** gehört `T` nur noch B

##### Scenario: Bereinigung nach Ablauf
- **GIVEN** eine Registrierung wurde seit mehr als 60 Tagen nicht aktualisiert
- **WHEN** die Bereinigung läuft (Zeit über `TimeProvider`)
- **THEN** ist sie gelöscht, jüngere Registrierungen bleiben

#### Requirement: Registrierungen beim Löschen des Kontos entfernen
Das System SHALL beim Löschen eines Nutzerkontos alle Geräte-Token und Web-Subscriptions dieses Nutzers mitlöschen (inklusive offener Outbox-Einträge der Zustellung). **Abhängigkeit:** Ein Konto-Löschen existiert derzeit nicht (in `profil-einstellungen` ausdrücklich ausgeschlossen, als Folge-Epic vorgesehen). Bis dahin SHALL die Löschung der Registrierungen auf Anwendungsebene so vorbereitet sein (Repository-Methode „alle Registrierungen eines Nutzers löschen“, getestet); die Verdrahtung erfolgt mit dem Konto-Löschen.

##### Scenario: Konto gelöscht
- **GIVEN** ein Nutzer mit drei Registrierungen
- **WHEN** sein Konto gelöscht wird
- **THEN** existiert keine Registrierung dieses Nutzers mehr und es werden keine Push-Nachrichten mehr an diese Geräte gesendet

##### Scenario: Andere Nutzer unberührt
- **GIVEN** zwei Nutzer mit Registrierungen
- **WHEN** das Konto von Nutzer A gelöscht wird
- **THEN** bleiben die Registrierungen von Nutzer B bestehen

##### Scenario: Anlegen
- **GIVEN** ein angemeldeter Nutzer
- **WHEN** er `PUT …/devices/{id}` mit gültigem Token, Plattform `ios` aufruft
- **THEN** antwortet die API mit 204 und das Gerät ist ihm zugeordnet

##### Scenario: Aktualisieren
- **GIVEN** ein vorhandenes Gerät
- **WHEN** erneut mit anderem Token `PUT` aufgerufen wird
- **THEN** ersetzt das neue Token das alte, es entsteht kein zweiter Eintrag

##### Scenario: Fremdes Gerät
- **GIVEN** `installationId` gehört Nutzer A
- **WHEN** Nutzer B darauf `DELETE` aufruft
- **THEN** antwortet die API mit 404 und löscht nichts

##### Scenario: Ohne Anmeldung
- **GIVEN** kein Access-Token
- **WHEN** `PUT` oder `DELETE` aufgerufen wird
- **THEN** antwortet die API mit 401

##### Scenario: Ungültige Plattform
- **GIVEN** Plattform `windows`
- **WHEN** `PUT` aufgerufen wird
- **THEN** antwortet die API mit 400 und Feldmeldung `platform`

#### Requirement: Push-Zustellung der Hinweise (Backend)
Das System SHALL für jede neu gespeicherte Benachrichtigung (Typ `CriticalReading`, `MeasurementReminder`) eine Push-Nachricht an alle registrierten Geräte des Nutzers senden. Titel und Text SHALL in der gespeicherten Kultur des Geräts stehen, die Kategorie, aber keine Messwerte, Profildaten oder personenbezogene Angaben enthalten (Sperrbildschirm-Vorschau, ADR 0003) und bei `CriticalReading` den Hinweis „keine ärztliche Beratung“ tragen. Ein Push-Fehler SHALL die Messung und die gespeicherte Benachrichtigung nicht beeinträchtigen. Ungültige Tokens/Subscriptions (vom Anbieter gemeldet, z. B. 404/410) SHALL gelöscht werden.

**Kritische Hinweise (`CriticalReading`):** Der Versand SHALL persistent erfolgen (Outbox): Der Zustellauftrag je Gerät wird in derselben Transaktion wie Messung und Benachrichtigung gespeichert, mit Status (`Pending`, `Sent`, `Failed`), Versuchszahl, nächstem Versuchszeitpunkt und letztem Fehlercode (ohne Token). Ein Dispatcher SHALL offene Aufträge nach Neustart weiterverarbeiten und mit Backoff wiederholen, bis `Sent` (Annahme durch den Anbieter) oder die maximale Zahl Versuche/Dauer erreicht ist (`Failed`, Werte konfigurierbar, Festlegung im ADR). Eine Zustellbestätigung gilt als Annahme durch den Anbieter; eine Geräte-Empfangsbestätigung ist nicht gefordert (siehe Offene Frage 8a). Der Zustellstatus SHALL für Betrieb/Tests abfragbar sein und ein dauerhaftes `Failed` SHALL als Warnung geloggt werden (ohne Token, ohne Messwerte). Mehrinstanzbetrieb: Aufträge dürfen nicht doppelt gesendet werden (Sperre je Auftrag, z. B. `FOR UPDATE SKIP LOCKED`).

**Unkritische Hinweise (`MeasurementReminder`):** Best Effort mit Retry (z. B. Polly), keine Persistenz der Aufträge.

##### Scenario: Kritischer Wert
- **GIVEN** ein Nutzer mit einem registrierten Gerät
- **WHEN** eine Messung der Kategorie Grad 3 gespeichert wird
- **THEN** erhält das Gerät genau eine Push-Nachricht ohne Zahlenwerte und mit Haftungshinweis

##### Scenario: Erinnerung fällig
- **GIVEN** eine Erinnerung 08:00 und ein registriertes Gerät
- **WHEN** der `ReminderWorker` die Erinnerung auslöst
- **THEN** wird eine Push-Nachricht gesendet; sie wird höchstens einmal je Auslösung gesendet

##### Scenario: Kein Gerät
- **GIVEN** ein Nutzer ohne Gerät
- **WHEN** eine Benachrichtigung entsteht
- **THEN** wird sie wie bisher nur gespeichert, ohne Fehler

##### Scenario: Anbieter nicht erreichbar
- **GIVEN** der Push-Anbieter antwortet mit einem Fehler
- **WHEN** eine Benachrichtigung entsteht
- **THEN** sind Messung und Benachrichtigung gespeichert, der Fehler ist ohne Token im Log, Retry greift

##### Scenario: Kritischer Hinweis wird persistent geplant
- **GIVEN** ein Nutzer mit zwei Registrierungen
- **WHEN** eine Grad-3-Messung gespeichert wird
- **THEN** existieren in derselben Transaktion zwei Zustellaufträge mit Status `Pending`

##### Scenario: Neustart während der Zustellung
- **GIVEN** ein `Pending`-Auftrag und ein Neustart der API
- **WHEN** der Dispatcher wieder läuft
- **THEN** wird der Auftrag gesendet und danach `Sent` gesetzt

##### Scenario: Anbieter vorübergehend nicht erreichbar (kritisch)
- **GIVEN** der Anbieter antwortet mit 5xx
- **WHEN** der Dispatcher den Auftrag verarbeitet
- **THEN** bleibt er `Pending` mit erhöhter Versuchszahl und späterem Versuchszeitpunkt (Backoff)

##### Scenario: Versuche erschöpft
- **GIVEN** die maximale Zahl Versuche ist erreicht
- **WHEN** der letzte Versuch scheitert
- **THEN** ist der Status `Failed` und es wird eine Warnung ohne Token geloggt

##### Scenario: Keine Doppelzustellung
- **GIVEN** zwei Dispatcher-Instanzen
- **WHEN** beide denselben `Pending`-Auftrag sehen
- **THEN** sendet nur eine Instanz ihn

##### Scenario: Unkritische Erinnerung
- **GIVEN** der Anbieter schlägt fehl
- **WHEN** eine Messerinnerung zugestellt werden soll
- **THEN** werden Versuche begrenzt wiederholt, es wird kein Auftrag persistiert

##### Scenario: Ungültiges Token
- **GIVEN** der Anbieter meldet das Token als nicht (mehr) gültig
- **WHEN** der Versand scheitert
- **THEN** wird das Gerät gelöscht

#### Requirement: Push öffnet die Hinweise (MAUI)
Das System SHALL beim Antippen einer Push-Nachricht die App auf die Seite „Hinweise“ führen (bei fehlender Anmeldung zuerst Anmeldung, danach Hinweise) und den Ungelesen-Zähler aktualisieren. Eine im Vordergrund eintreffende Push-Nachricht SHALL den Zähler aktualisieren und per `SemanticScreenReader.Announce` angesagt werden, ohne die Bedienung zu unterbrechen.

##### Scenario: Antippen aus dem Sperrbildschirm
- **GIVEN** die App ist beendet und der Nutzer angemeldet
- **WHEN** er die Push-Nachricht antippt
- **THEN** öffnet die App die Hinweise-Seite mit aktuellem Stand

##### Scenario: Im Vordergrund
- **GIVEN** die App ist offen
- **WHEN** ein Push eintrifft
- **THEN** steigt der Zähler und die Änderung wird angesagt

#### Requirement: Einstellung in „Profil & Einstellungen“ (MAUI)
Das System SHALL im Abschnitt „Benachrichtigungen“ der Profilseite einen Schalter „Push-Hinweise“ mit Statustext, den Erklärtext (Datenschutz: welche Daten das Gerät verlassen – Geräte-Token, Plattform, Sprache – und dass die Nachrichten keine Messwerte enthalten) und die Aktion „Systemeinstellungen öffnen“ (nur bei abgelehnter Berechtigung) anzeigen. Die Wahl „aktiv“ wird je Gerät lokal gespeichert (`Preferences`); der Server kennt nur das registrierte Token.

##### Scenario: Anzeige
- **GIVEN** ein angemeldeter Nutzer
- **WHEN** er die Profilseite öffnet
- **THEN** sieht er Schalter, aktuellen Status und den Datenschutztext

#### Requirement: Web-Push in der Blazor-Oberfläche
Das System SHALL in der Blazor-Oberfläche (Profil & Einstellungen, Abschnitt „Benachrichtigungen“) Web-Push anbieten, sofern der Browser Service Worker, Push-API und Notification-API unterstützt; sonst SHALL ein erklärender Hinweis statt des Schalters erscheinen. Es SHALL einen Service Worker (in `wwwroot`) geben, der Push-Ereignisse empfängt, eine Benachrichtigung anzeigt und beim Klick die Seite `/notifications` öffnet bzw. fokussiert. Der öffentliche VAPID-Schlüssel SHALL aus der Konfiguration kommen (Backend liefert ihn, z. B. `GET /api/v1/devices/web-push-key`, oder `wwwroot/appsettings.json`); der private Schlüssel SHALL nur im Backend über Environment Variables/Secret Manager liegen. Die Einwilligung (Erklärtext mit „Aktivieren“/„Nicht jetzt“) SHALL wie in MAUI vor der Browser-Berechtigungsabfrage erfolgen, die Abfrage SHALL nur nach Klick des Nutzers (nie beim Laden) ausgelöst werden. Nach erteilter Berechtigung SHALL `pushManager.subscribe` mit dem VAPID-Schlüssel erfolgen und die Subscription über `PUT /api/v1/devices/{installationId}` (Plattform `web`) registriert werden. Beim Ausschalten, Abmelden oder Berechtigungsentzug SHALL `unsubscribe()` aufgerufen und `DELETE` gesendet werden (vor dem Löschen der Tokens). Die `installationId` SHALL je Browser-Profil lokal (`localStorage`) gespeichert werden. Der Service Worker SHALL keine Anmeldedaten und keine Messwerte speichern. Der Push-Text enthält keine Messwerte (siehe Zustellung).

##### Scenario: Aktivieren
- **GIVEN** ein angemeldeter Nutzer in einem unterstützten Browser
- **WHEN** er „Push-Hinweise aktivieren“ und im Erklärtext „Aktivieren“ wählt und die Browser-Abfrage bestätigt
- **THEN** wird eine Subscription erzeugt, per `PUT` mit Plattform `web` registriert und der Status „Push-Hinweise aktiv“ angezeigt

##### Scenario: Nicht jetzt / keine automatische Abfrage
- **GIVEN** die Blazor-Oberfläche wird geladen oder der Erklärtext mit „Nicht jetzt“ geschlossen
- **WHEN** der Nutzer weiterarbeitet
- **THEN** wurde keine Browser-Berechtigungsabfrage ausgelöst und keine Subscription erzeugt

##### Scenario: Browser-Berechtigung blockiert
- **GIVEN** Notification-Berechtigung ist `denied`
- **WHEN** die Seite den Status ermittelt
- **THEN** wird kein `PUT` gesendet, der Status erklärt, wie die Berechtigung in den Browser-Einstellungen geändert wird

##### Scenario: Nicht unterstützter Browser
- **GIVEN** der Browser unterstützt keine Push-API
- **WHEN** die Einstellungsseite gerendert wird
- **THEN** erscheint ein erklärender Hinweis statt des Schalters, ohne Fehler

##### Scenario: Ausschalten und Abmelden
- **GIVEN** Web-Push ist aktiv
- **WHEN** der Nutzer den Schalter ausschaltet oder sich abmeldet
- **THEN** wird `unsubscribe()` aufgerufen und `DELETE …/devices/{installationId}` gesendet, bei Abmeldung vor dem Löschen der Tokens; scheitert das Netz, wird die Löschung vorgemerkt und nachgeholt

##### Scenario: Push empfangen und anklicken
- **GIVEN** ein Push trifft ein
- **WHEN** der Service Worker ihn verarbeitet und der Nutzer die Benachrichtigung anklickt
- **THEN** zeigt der Browser eine Benachrichtigung ohne Messwerte, und der Klick öffnet bzw. fokussiert `/notifications`

##### Scenario: Subscription abgelaufen
- **GIVEN** der Anbieter meldet die Subscription als ungültig (404/410)
- **WHEN** der Versand scheitert
- **THEN** wird sie serverseitig gelöscht, und beim nächsten Seitenaufruf mit aktivem Push erneuert die Seite die Subscription

#### Requirement: Plattformumfang
Das System SHALL Push für **iOS, Android und Web** unterstützen. Mac Catalyst und Windows SHALL NOT unterstützt werden: dort wird kein Push-Schalter angezeigt bzw. die Funktion ist ohne Fehler inaktiv (Erklärhinweis). Die Mindestversionen Android 21 und iOS 15 (aus der `.csproj`) bleiben bestehen.

##### Scenario: Nicht unterstützte Plattform
- **GIVEN** die App läuft unter Mac Catalyst oder Windows
- **WHEN** die Einstellungen geöffnet werden
- **THEN** erscheint kein Push-Schalter und es wird keine Berechtigung angefragt

#### Requirement: i18n und Barrierefreiheit der Push-Funktion
Das System SHALL alle neuen Texte (Erklärtext, Status, Schalter, Fehler, Ansagen) über `Strings`-`.resx` (DE/EN) ausgeben (Test auf Schlüsselparität). Bedienelemente SHALL `SemanticProperties` (Description/Hint) besitzen, mindestens 44 × 44 pt groß sein, per Tastatur/Screenreader bedienbar sein, Statuswechsel über `SemanticScreenReader.Announce` mitteilen und Zustände nicht allein über Farbe vermitteln. Fehler SHALL ohne Stacktrace oder Token angezeigt werden. Die Push-Texte des Servers SHALL in DE und EN vorliegen.

##### Scenario: Englisches Gerät
- **GIVEN** Gerätesprache Englisch
- **WHEN** die Push-Einstellungen angezeigt werden
- **THEN** sind alle Texte englisch und der Test auf Schlüsselparität ist grün

##### Scenario: Screenreader
- **GIVEN** VoiceOver oder TalkBack ist aktiv
- **WHEN** der Nutzer Push aktiviert bzw. deaktiviert
- **THEN** wird der neue Status einmalig angesagt und der Fokus bleibt am Schalter

### MODIFIED Requirements
_Entfällt._ (Das Requirement „Abmelden“ in `specs/maui-app.openspec.md` bleibt gültig; es wird durch „Token entfernen“ ergänzt, nicht ersetzt.)

### REMOVED Requirements
_Entfällt._

## 4. UI/UX Requirements
- **Komponenten:** `ProfilePage` (neuer Abschnitt „Benachrichtigungen“) mit `ProfileViewModel` oder eigenem `PushSettingsViewModel`; Erklärdialog über `IDialogService`; neue Abstraktionen in `Maui.Core` (`IPushRegistrationService`, `IPushPermissionService`, `IDeviceApi`), plattformspezifische Umsetzung in `Maui/Platforms/{iOS,Android}`.
- **State:** Berechtigungsstatus, „aktiv“-Wunsch (`Preferences`), `installationId` (zufällige GUID, lokal; keine Hardware-ID), Vormerkung „Token löschen“ für Offline-Fall. Das Token selbst wird nicht dauerhaft im Klartext abgelegt, sofern der Anbieter es nicht erneut liefert (`SecureStorage`).
- **i18n / a11y:** Schlüssel z. B. `Push_Title`, `Push_Explain`, `Push_Enable`, `Push_NotNow`, `Push_StatusActive|Off|Denied`, `Push_OpenSettings`, `Push_Error`; `SemanticProperties`, Ansagen, 44 × 44 pt.

## 5. API & Backend Requirements
- **Authentifizierung/Autorisierung:** Microsoft Identity (JWT), `[Authorize]`; nur eigene Geräte.
- **Endpoints (neu, `api/v1`):**
  - `PUT /api/v1/devices/{installationId}`: Body `{ token, platform: "ios"|"android", culture: "de"|"en" }`, 204 / 400 / 401.
  - `DELETE /api/v1/devices/{installationId}`: 204 / 401 / 404.
- **Modelle/Entitäten (PostgreSQL, neue Migration, bestehende nicht ändern):**
  - `DeviceRegistration`: `Id` (Guid, = installationId), `UserId`, `Token` (string, eindeutig; bei `web` der Subscription-Endpoint), `P256dh`/`Auth` (nur `web`), `Platform` (`ios`/`android`/`web`), `Culture`, `CreatedAt`, `UpdatedAt`. Index auf `UserId`.
  - `PushDelivery` (Outbox, nur kritische Hinweise): `Id`, `NotificationId`, `DeviceRegistrationId`, `Status` (`Pending`/`Sent`/`Failed`), `Attempts`, `NextAttemptAt`, `LastError` (Code, ohne Token), `CreatedAt`, `SentAt`.
  - Zusätzlicher Endpunkt für Web: öffentlicher VAPID-Schlüssel (siehe Offene Frage 13).
- **Zustellung:** Port in Application (z. B. `IPushSender`), Implementierung in Infrastructure (FCM für Android, APNs für iOS, ggf. FCM für beide); Zugangsdaten nur über Environment Variables/Secret Manager, nie im Code.
- **OpenAPI:** neue Endpunkte mit `problem+json`-Beispielen dokumentieren (wie bestehende).
- Backend-Aufgabe nötig, da in Epic 6 (T6.1–T6.4) nicht vorhanden.

## 6. KI/AI Requirements (Optional)
Entfällt.

## 7. Design (Technischer Ansatz)
- Neuer ADR `docs/adr/0004-push-notifications.md` (architect-agent) vor der Umsetzung: Anbieterwahl (FCM direkt für beide Plattformen vs. FCM + APNs), Versand-Mechanik (nach Commit, Fehler dürfen die Transaktion nicht beeinflussen; Outbox ja/nein, vgl. ADR 0003 zu Mehrinstanzbetrieb), Retry, Token-Bereinigung.
- Outbox für kritische Hinweise: Auftrag je Registrierung in der Transaktion der Messung; Dispatcher als `BackgroundService` mit Sperre je Auftrag. Unkritische Erinnerungen bleiben im bestehenden `ReminderProcessor`-Pfad ohne Persistenz. Das erweitert ADR 0003 (Mehrinstanzbetrieb) und ist im neuen ADR zu begründen.
- Web: Service Worker und Subscription über schlanke JS-Interop (`IPushInterop`) hinter einem Interface, damit bUnit mit Fakes testet.
- Client-Zerlegung testbar: Logik (Einwilligung, Statusmaschine, Registrierung, Entfernen) in `Maui.Core` ohne MAUI-Abhängigkeit mit Fakes; plattformnahe Teile (Berechtigung, Token-Abruf, Systemeinstellungen) hinter Interfaces.
- Das Entfernen beim Abmelden muss vor `SessionService.ClearAsync` laufen; `ClearAsync` wird auch bei fehlgeschlagener Token-Erneuerung aufgerufen (dort ist ein authentifizierter DELETE nicht möglich, daher Vormerkung/serverseitige Umzuordnung).
- Datenschutz: Push-Inhalte ohne Messwerte und Profildaten; Provider (Google/Apple) erfahren Token und Nachrichtentext, daher Text knapp und ohne Gesundheitswerte halten. Rechtsgrundlage Einwilligung, Widerruf jederzeit.
- Risiken: Apple Developer Account und Firebase-Projekt nötig; Simulatoren liefern ggf. kein echtes iOS-Token; Mehrinstanzbetrieb kann Doppel-Pushes erzeugen (ADR 0003).

## 8. Tasks (Implementierungs-Checkliste)
- [ ] 1. (architect) ADR 0004 Push-Anbieter, Versand, Datenschutz; Offene Fragen klären
- [ ] 2. (backend) Tests (Red) für Geräte-Endpunkte, Zuordnung, Fremdzugriff, Validierung, Zustellung (mit Fake `IPushSender`)
- [ ] 3. (backend) Domain/Application/Infrastructure: `DeviceRegistration`, Handler, Migration, `IPushSender`-Implementierung, Hook bei neuen Benachrichtigungen (kritischer Wert, `ReminderProcessor`), Token-Bereinigung, Retry
- [ ] 4. (backend) `DevicesController`, OpenAPI inkl. `problem+json`-Beispielen
- [ ] 5. (backend/frontend) Contracts: `IDeviceApi` (Refit) und DTOs
- [ ] 6. (sec) Review: Secrets, Token nicht im Log, Eigentümerprüfung, Datenschutz der Push-Texte
- [ ] 7. (frontend) Tests (Red) in `Tests/CleanArchitecture.Maui.Tests`: Einwilligung, Statusmaschine, Registrierung/Wechsel/Entfernen, Offline-Vormerkung, Abmelden-Reihenfolge, DE/EN-Parität
- [ ] 8. (frontend) `Maui.Core`-Services und ViewModel; `ProfilePage`-Abschnitt; `Strings.resx` DE/EN
- [ ] 9. (frontend) Plattformcode iOS (Capability, Entitlements, Registrierung) und Android (`POST_NOTIFICATIONS`, Firebase-Konfiguration); Tippen auf Push öffnet Hinweise
- [ ] 10. (ux) A11y-Review: Erklärtext, Schalter, Ansagen, Zielgrößen
- [ ] 11. (devops) Secrets/Konfiguration für FCM/APNs in Aspire/CD, Dokumentation der Einrichtung
- [ ] 12. (qa) Test auf echten Geräten (iOS, Android 13+ und älter), Protokoll in `docs/`; E2E gegen die API
- [ ] 13. (doc) README/`docs` um Push-Einrichtung ergänzen
- [ ] 14. (backend) Gerätelimit 5, Umordnung von Token/Endpoint, Bereinigung nach 60 Tagen (Hosted Service, `TimeProvider`), Tests (Red) zuerst
- [ ] 15. (backend) Registrierungen auf Plattform `web` erweitern (Subscription-Felder, Validierung), VAPID-Konfiguration (privater Schlüssel nur per Secret), Web-Push-Sender (hinter `IPushSender`)
- [ ] 16. (backend) Outbox für kritische Hinweise: Entität `PushDelivery` (Status, Versuche, nächster Versuch, Fehlercode), Eintrag in derselben Transaktion, Dispatcher mit Backoff und Sperre je Auftrag (`SKIP LOCKED`), Migration, Tests (Neustart, Backoff, `Failed`, Doppelzustellung); Ergänzung von ADR 0003 zum Mehrinstanzbetrieb
- [ ] 17. (backend) Repository-Methode „alle Registrierungen und offenen Aufträge eines Nutzers löschen“, getestet; Verdrahtung mit Konto-Löschen, sobald dieses existiert (Abhängigkeit, siehe Requirement)
- [ ] 18. (frontend) Web: Tests (bUnit, Interop-Fake) für Einwilligung, Statuszustände, Subscribe/Unsubscribe, Abmelden-Reihenfolge, Vormerkung, nicht unterstützter Browser, DE/EN-Parität; `service-worker.js`, JS-Interop `IPushInterop`, Abschnitt in `Profile.razor`, `SharedResource` DE/EN, ARIA (`fieldset`/`legend`, `aria-live`, Labels, Tastatur)
- [ ] 19. (qa) Web-Push im Browser prüfen (Chrome, Firefox, Safari soweit unterstützt), axe-Lauf für den neuen Abschnitt, Eintrag in `docs/a11y/`
- [ ] 20. (devops/architect) Zielplattformen in `CleanArchitecture.Presentation.Maui.csproj` klären: `net10.0-maccatalyst` und `net10.0-windows…` sind dort als Targets enthalten, Push ist dort nicht unterstützt; ob die Targets entfernt werden, ist separat zu entscheiden (nicht Teil dieser Change)

## 9. Acceptance Criteria (DoD)
- [ ] Jedes Scenario aus Abschnitt 3 ist durch einen automatisierten Test abgedeckt und grün (Plattformteile: dokumentierter manueller Test auf Gerät).
- [ ] Backend (C# 14) umgesetzt und getestet (TUnit); neue Migration, keine Änderung bestehender Migrationen.
- [ ] MAUI-ViewModel-/Service-Tests grün; Seite gestylt, DE/EN vollständig, Schlüsselparität geprüft.
- [ ] Keine Messwerte, Profildaten oder Tokens in Push-Text, Logs, Fehlermeldungen, Exporten.
- [ ] Push ist ausschließlich nach Einwilligung aktiv; Abmelden und Ausschalten entfernen das Token nachweislich.
- [ ] Keine Secrets im Repository (inkl. privatem VAPID-Schlüssel).
- [ ] Web-Push funktioniert im Browser (Service Worker, Subscription, Entfernen); axe-Lauf für den neuen Abschnitt ohne „serious/critical“.
- [ ] Kritische Hinweise werden über die Outbox zugestellt (Neustart-, Backoff-, Failed- und Doppelzustellungstests grün); unkritische bleiben Best Effort mit Retry.
- [ ] Höchstens 5 Registrierungen je Nutzer, Bereinigung nach 60 Tagen und Löschung aller Registrierungen eines Nutzers getestet; Verdrahtung mit Konto-Löschen offen, bis dieses existiert.
- [ ] Mac Catalyst/Windows zeigen keinen Push-Schalter.
- [ ] Swagger/OpenAPI zeigt die neuen Endpunkte mit Fehlerbeispielen.
- [ ] A11y-Prüfung (VoiceOver/TalkBack) für den neuen Abschnitt protokolliert.
- [ ] Spec archiviert (Status `archived`), Requirements in `UserStories/specs/maui-app.openspec.md` bzw. neue Capability-Spec übernommen; Epic 6 T6.5 abgehakt.

## 10. Offene Fragen
1. **Push-Anbieter:** Nur FCM (auch für iOS über APNs-Anbindung) oder FCM + direkt APNs? Wer stellt Firebase-Projekt und Apple-Zugang (Developer Account, APNs-Schlüssel) bereit?
2. **Web-Push: Entschieden.** Ja, die Blazor-Oberfläche erhält Web-Push (Service Worker, Push-API/VAPID); das Backend bildet Web-Subscriptions ab.
3. **Gerätelimit: Entschieden.** Höchstens 5 Registrierungen je Nutzer (Geräte inkl. Web). Offen bleibt (Frage 12) das Verhalten bei Überschreitung.
4. **Inhalt der Push-Nachricht:** Reicht ein generischer Text mit Kategorie („Erhöhter Blutdruck erkannt, bitte prüfen. Keine ärztliche Beratung.“) oder soll der Text nur „Neuer Hinweis in der App“ lauten (maximaler Datenschutz auf dem Sperrbildschirm)? Wer formuliert die finalen DE/EN-Texte, und wird die Sprache der Gerätekultur oder der App-Spracheinstellung (nur lokal gespeichert) verwendet?
5. **Rechtliches:** Genügt der Erklärtext als Einwilligung, oder wird eine Datenschutzerklärung/Freigabe benötigt (Gesundheitsdaten, Übermittlung an Google/Apple)?
6. **Konto löschen: Entschieden.** Beim Löschen des Kontos werden alle Geräte-Token/Subscriptions mitgelöscht. Abhängigkeit: Ein Konto-Löschen existiert noch nicht (Folge-Epic laut `profil-einstellungen`); Task 17 bereitet die Löschung vor.
7. **Abmelden ohne Netz / fehlgeschlagene Token-Erneuerung: Entschieden.** Lokale Vormerkung plus Umzuordnung des Tokens beim nächsten Nutzer, zusätzlich serverseitige Bereinigung nach Ablauf (60 Tage ohne Aktualisierung, konfigurierbar).
8. **Zustellgarantie: Entschieden.** Kritische Hinweise: Outbox mit Retry, Persistenz und Zustellstatus (Pflicht). Unkritische: Best Effort mit Retry. Offen bleibt 8a.
8a. **Reichweite der Zustellbestätigung:** Genügt als „zugestellt“ die Annahme durch FCM/APNs/Web-Push-Dienst (so spezifiziert), oder wird zusätzlich eine Empfangsbestätigung vom Gerät gefordert? Welche Werte gelten für maximale Versuche, Backoff und Gesamtdauer?
9. **Zielplattformen: Entschieden.** Nur iOS, Android und Web; Mac Catalyst und Windows werden nicht unterstützt. Ob die Targets aus der `.csproj` entfernt werden, ist separat zu klären (Task 20).
10. **Mindestversionen: Entschieden.** Android 21 und iOS 15 reichen.
11. **Erinnerungen vs. lokale Benachrichtigung:** Epic 6 liefert Erinnerungen serverseitig; sollen sie ohne Netz zusätzlich lokal geplant werden? Hier ausgeschlossen.
12. **Verhalten beim sechsten Gerät:** Ablehnen (Vorschlag 409) oder ältestes Gerät ersetzen? Neu durch Frage 3.
13. **Web-Push-Anbieter und Browser:** Welche Browser gelten als Mindestumfang (Safari/iOS-Web-Push nur als installierte Web-App)? Wo wird der öffentliche VAPID-Schlüssel ausgeliefert (Backend-Endpunkt oder `appsettings.json`)? Betrifft Frage 1 mit.
