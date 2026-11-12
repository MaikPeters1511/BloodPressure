# ADR 0006: E-Mail-Bestätigung, MFA und Passkeys für Konten

- **Status:** teilweise accepted. Vom User entschieden (siehe „Entscheidung“, Stand 2026-11-12): E-Mail-Versand per MailKit, MFA freiwillig mit TOTP zuerst, Bestandskonten per Migration als bestätigt, Passkeys erst Web, MAUI später. Alles andere bleibt `proposed` und hängt an den noch „Offenen Fragen an den User“. Nicht umgesetzt (Risiko R-07 in `docs/security/owasp-epic-01.md`).
- **Bezug:** ADR 0001 (Identity-API), `docs/security/owasp-epic-01.md` (R-07, R-06, R-02), `CleanArchitecture.Presentation.API/Program.cs`.
- **Geprüft am:** 2026-11-12, lokal auf dem Rechner des Architekten. Jede Aussage unter „Geprüfte Fakten“ stammt aus einem ausgeführten Befehl; was nicht geprüft werden konnte, steht unter „Nicht geprüft“.

## Kontext

Die Registrierung über `MapIdentityApi<IdentityUser>()` (`Program.cs`, Gruppe `/api/auth`) ist offen: keine E-Mail-Bestätigung (`RequireConfirmedEmail` ist nicht gesetzt), keine Zwei-Faktor-Authentifizierung, kein Passkey. Es gibt drei Clients: die API selbst, die Blazor-WebAssembly-UI und die MAUI-App (iOS/Android). Die App verarbeitet Gesundheitsdaten (Blutdruck). Zusätzlich verknüpft der Register-Filter in `Program.cs` die Altdaten beim erfolgreichen Registrieren mit dem ersten Nutzer (`LegacyDataClaimer`, R-06); wer sich zuerst registriert, bekommt sie, auch mit einer fremden E-Mail-Adresse.

Randbedingungen aus dem Repo (geprüft): Zielframework `net10.0`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14, `AddPostgresSqlDbContext : IdentityDbContext<IdentityUser>`, MAUI-Controls 10.0.110 (`net10.0-android;net10.0-ios;net10.0-maccatalyst`, `SupportedOSPlatformVersion` iOS 15.0, Android 21.0 laut MAUI-Standard-Props), `ApplicationId` der MAUI-App ist aktuell `BloodPressureMaui`. Auf der Maschine sind Runtime 10.0.14 und 11.0.0 installiert; maßgeblich ist 10.0.14 (Repo-Version).

## Geprüfte Fakten

Methode: Reflection und ein ausführbares Wegwerfprojekt (Identity 10.0.14, `AddIdentityApiEndpoints` + `MapGroup("/api/auth").MapIdentityApi`, In-Memory-DB; liegt nur im Scratchpad, nicht im Repo), dazu Zeichenketten-Extraktion aus `Microsoft.AspNetCore.Identity.dll` der Runtime 10.0.14.

1. **Passkeys im Identity-Kern: ja.** In 10.0.14 existieren `IdentityPasskeyOptions` (`ServerDomain`, `ChallengeSize`, `AuthenticatorTimeout`, `UserVerificationRequirement`, `ResidentKeyRequirement`, `AttestationConveyancePreference`, `AuthenticatorAttachment`, `IsAllowedAlgorithm`, `ValidateOrigin`, `VerifyAttestationStatement`), `SignInManager` mit `MakePasskeyCreationOptionsAsync`, `MakePasskeyRequestOptionsAsync`, `PerformPasskeyAttestationAsync`, `PerformPasskeyAssertionAsync`, `PasskeySignInAsync`, `UserManager` mit `AddOrUpdatePasskeyAsync`, `GetPasskeysAsync`, `FindByPasskeyIdAsync`, `RemovePasskeyAsync`, `UserPasskeyInfo`, `IUserPasskeyStore<TUser>` und die Entität `IdentityUserPasskey<TKey>`. `IdentityDbContext` hat die Eigenschaft `UserPasskeys`. Der Identity-Kern verarbeitet also die serverseitige WebAuthn-Prüfung (Attestation/Assertion).
2. **Passkey-Endpunkte in `MapIdentityApi`: nein.** Registrierte Routen unter `/api/auth` (aufgelistet): `POST register`, `POST login`, `POST refresh`, `GET confirmEmail`, `POST resendConfirmationEmail`, `POST forgotPassword`, `POST resetPassword`, `POST manage/2fa`, `GET/POST manage/info`. Kein Passkey-, Credential- oder WebAuthn-Endpunkt. Die Passkey-Funktionen sind Bausteine (SignInManager/UserManager); die HTTP-Endpunkte für Optionen, Registrierung und Anmeldung müssen wir selbst als Minimal-APIs schreiben. In `IdentityOptions` gibt es in 10.0.14 keine Eigenschaft `Passkey` (Eigenschaften: ClaimsIdentity, User, Password, Lockout, SignIn, Tokens, Stores); wie `IdentityPasskeyOptions` konfiguriert wird (z. B. `Configure<IdentityPasskeyOptions>`), ist nicht geprüft. Die Fehlermeldung „This operation requires an IPasskeyHandler service to be registered“ zeigt, dass ein `IPasskeyHandler` im DI registriert sein muss; ob `AddIdentityApiEndpoints` ihn registriert, ist nicht geprüft.
3. **Schema:** In den vorhandenen Migrationen (`CleanArchitecture.Infrastructure/Migrations`) gibt es keine Tabelle `AspNetUserPasskeys`. Passkeys brauchen eine neue Migration (bestehende nicht ändern, CLAUDE.md).
4. **2FA in `MapIdentityApi`:** Es gibt `POST manage/2fa` (Request mit `enable`, `twoFactorCode`, `resetSharedKey`, `resetRecoveryCodes`, `forgetMachine`; Antwort enthält `sharedKey`, `recoveryCodesLeft`, `recoveryCodes`, `isTwoFactorEnabled`, `isMachineRemembered`) und im Login-Request die Felder `twoFactorCode` und `twoFactorRecoveryCode` sowie die Meldung `RequiresTwoFactor`. Das ist TOTP (Authenticator-App) mit Recovery-Codes ohne eigenen Code. Der Ablauf selbst (Aktivieren, Login mit Code) wurde nicht ausgeführt, nur über die vorhandenen Bezeichner belegt. Es gibt keinen Endpunkt, der einen QR-Code liefert; die App müsste aus `sharedKey` selbst eine `otpauth://`-URI bauen.
5. **E-Mail-Bestätigung, ausgeführt:** Mit `SignIn.RequireConfirmedEmail = true`:
   - `register` antwortet 200 ohne Inhalt und ruft `IEmailSender<IdentityUser>.SendConfirmationLinkAsync` (Link auf `GET /api/auth/confirmEmail?userId=…&code=…`). Der Standard-Sender ist ein No-Op; ohne eigene Registrierung kommt keine Mail an.
   - `login` vor der Bestätigung: 401 mit `detail: "NotAllowed"`, **auch bei falschem Passwort**. Ein unbekannter Nutzer liefert 401 mit `detail: "Failed"`. Das ist ein Konto-Enumerations-Orakel: „NotAllowed“ verrät ein existierendes, unbestätigtes Konto.
   - Zweite Registrierung derselben Adresse: 400 mit `errors.DuplicateUserName` („Username … is already taken“). Das ist die in R-07 genannte Enumeration; `RequireConfirmedEmail` ändert daran nichts.
   - `resendConfirmationEmail` antwortet für bekannte und unbekannte Adressen identisch mit 200 (kein Orakel). Nach `confirmEmail` (200) ist der Login erfolgreich (Bearer- und Refresh-Token).
6. **Folgen für den bestehenden Client-Code:** `AuthSession.RegisterAsync` meldet nach der Registrierung sofort an (`Services/AuthSession.cs`, Aufruf von `LoginAsync` direkt danach). Mit `RequireConfirmedEmail` schlägt dieser Login fehl; UI und MAUI-App müssen den Zwischenzustand „Bestätigungs-Mail unterwegs“ kennen. Der Altdaten-Filter hängt an `register` und läuft damit schon vor der Bestätigung (siehe Risiken).
7. **Bestehender Stand im Repo:** Strengeres Rate-Limit für `/api/auth/*` existiert bereits (`RateLimiting:AuthPermitLimitPerMinute`, Standard 10, je IP). Ein Mail- oder SMTP-Code, eine Mail-Aspire-Ressource und MailKit/MimeKit sind weder im Repo noch im lokalen NuGet-Cache. Der AppHost enthält nur Postgres (mit PgAdmin) und die API. Tests nutzen `Tests/CleanArchitecture.Tests/TestAuthHandler.cs` über `ApiFactory`, also nicht die echte Identity-Anmeldung; der Registrierungs- und Login-Pfad ist dort nicht ausgeübt.

## Nicht geprüft

- Das Verhalten von Passkey-Aufrufen (Optionen erzeugen, Attestation/Assertion prüfen) wurde nicht ausgeführt, nur die vorhandenen Typen und Methoden gelistet. Ob die EF-Core-Stores aus `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.14 `IUserPasskeyStore` für `AddPostgresSqlDbContext` ohne Zusatzcode erfüllen und welches Migrationsschema entsteht, ist offen (Phase-3-Spike).
- TOTP-Ablauf (Aktivieren, Login mit Code, Wirkung auf Refresh-Token und SecurityStamp) nicht ausgeführt.
- Blazor-WASM-WebAuthn und MAUI-Passkeys: kein Code geschrieben, keine Geräte, kein Simulator. Die Angaben dazu unten sind Wissen aus der Plattformdokumentation, nicht in dieser Session verifiziert. Konkrete Mindestversionen (iOS-Version für Passkeys mit `ASAuthorizationPlatformPublicKeyCredentialProvider`, Android-Version bzw. Play-Services für Credential Manager/Jetpack `androidx.credentials`) und ein Binding-Paket für MAUI sind vor der Umsetzung zu prüfen.
- Ob `MapIdentityApi` unter .NET 11 (vorhandene Runtime 11.0.0) Passkey-Endpunkte hat, wurde nicht untersucht; die Repo-Version ist .NET 10.
- SMTP-Anbieter, Zustellbarkeit (SPF/DKIM/DMARC), Domain des Produktivbetriebs: unbekannt.

## Entscheidungstreiber

- Gesundheitsdaten (DSGVO Art. 9), Konto-Übernahme ist schwerwiegend; Schutz gegen unbestätigte fremde Adressen und Credential Stuffing.
- Minimaler Eigencode: möglichst Identity-Bordmittel (CLAUDE.md: Microsoft Identity, OWASP).
- Drei Clients mit unterschiedlicher Passkey-Technik; Blazor und MAUI teilen `CleanArchitecture.Contracts` (`IBloodPressureApi`), neue Endpunkte gehören dort hinein.
- Keine Geheimnisse im Code; Konfiguration per Umgebungsvariablen/Secrets, lokal per Aspire.
- Bestehende Konten und Altdaten dürfen nicht unbeabsichtigt ausgesperrt oder fremd zugeordnet werden.
- Tests vor Implementierung (TDD), jedes Scenario ein Test.

## Optionen

### A. E-Mail-Bestätigung und Versand

| Option | Vorteile | Nachteile |
|---|---|---|
| A1: `RequireConfirmedEmail = true` + eigener `IEmailSender<IdentityUser>` (Infrastructure) über **MailKit** (`SmtpClient`) | Etablierte, gepflegte SMTP-Bibliothek, STARTTLS/SSL, saubere Zertifikatsprüfung, async | Neue NuGet-Abhängigkeit (Lizenz/Update-Pflege, Dependabot deckt NuGet ab) |
| A2: wie A1, aber `System.Net.Mail.SmtpClient` | Keine Abhängigkeit | Von Microsoft als nicht empfohlen markiert (Hinweis aus der Dokumentation, hier nicht neu geprüft), schwächer bei modernen Authentifizierungsverfahren |
| A3: Transaktions-Mail-Anbieter über HTTP-API statt SMTP | Zustellbarkeit, Statistiken | Widerspricht der Vorgabe „SMTP“ des Users, Anbieterbindung |
| A4: Keine Bestätigung, stattdessen Einladungscode (R-07-Vorschlag) | Kein Mailversand, kein Self-Service | Entspricht nicht dem Wunsch des Users, kein Konto-Besitznachweis der Adresse |

Gemeinsam für A1/A2: `IEmailSender<IdentityUser>` ist der Erweiterungspunkt (geprüft). Der Link zeigt auf die API (`/api/auth/confirmEmail`); für Web und MAUI ist eine Landeseite oder ein Deep Link sinnvoller als eine rohe API-Antwort (siehe Offene Fragen). Lokal fängt ein Mail-Container (Papercut, MailPit o. Ä.) die Mails ab; welcher davon im Aspire-Katalog als fertige Ressource verfügbar ist, wurde nicht geprüft (`~/.nuget/packages` enthält nur Postgres, Qdrant, SQL Server, JavaScript/NodeJS und Ollama-Hosting). Fallback ohne Aspire-Integration: `AddContainer("mail", "axllent/mailpit")` im AppHost, SMTP-Zugangsdaten als Umgebungsvariablen an die API (`Smtp__Host`, `Smtp__Port`, `Smtp__User`, `Smtp__Password`, `Smtp__From`).

### B. Zweiter Faktor

| Option | Vorteile | Nachteile |
|---|---|---|
| B1: **TOTP** mit Recovery-Codes über `manage/2fa` und `twoFactorCode`/`twoFactorRecoveryCode` im Login | Endpunkte vorhanden (geprüft), plattformunabhängig, funktioniert in Web und MAUI gleich, nur UI nötig | Phishing-anfällig, QR-Code/`otpauth://` selbst erzeugen, Nutzer brauchen eine Authenticator-App, Recovery-Codes müssen sicher aufbewahrt werden |
| B2: **Nur Passkeys** (kein TOTP) | Phishing-resistent, gute Bedienung | Keine Endpunkte vorhanden (geprüft), hoher Aufwand je Plattform, ohne Passkey-fähiges Gerät/Domain-Bindung kein Zugang, Wiederherstellung ungelöst |
| B3: **E-Mail-Code als zweiter Faktor** | Kein Zusatz-App | Faktor ist das Postfach, das auch für Wiederherstellung dient; ADR-Ziel „MFA“ wäre kaum erfüllt |

### C. Passkeys

| Option | Vorteile | Nachteile |
|---|---|---|
| C1: **Plattform-nativ** (Blazor WASM per `navigator.credentials` über JS-Interop, MAUI iOS per `ASAuthorization`, Android per Credential Manager) | Beste Bedienung, Passkey-Synchronisierung über iCloud-Schlüsselbund/Google Password Manager | Drei Implementierungen, MAUI braucht Plattformcode oder ein Binding, Domain-Verknüpfung nötig (`apple-app-site-association`, `assetlinks.json`), Geräte- und Simulatortests |
| C2: **Nur Web** (Passkey nur in Blazor, MAUI nutzt weiter Passwort + TOTP) | Ein Client, JS-Interop überschaubar | MAUI-Nutzer ohne Passkey; Entscheidung „Passkeys für alle Clients“ des Users nicht erfüllt |
| C3: Passkeys in MAUI über Systembrowser/WebAuthenticator mit der Blazor-Seite | Wiederverwendung der Web-Implementierung, kein nativer Code | Zusätzlicher Browser-Hop, Rückgabe des Tokens per Deep Link, Sicherheitsprüfung der Rückgabe nötig, schlechtere UX |

## Entscheidung (vorgeschlagen)

Belegt ist nur, was oben geprüft wurde. Punkte 1 bis 3 sind vom User entschieden, Punkt 4 (Enumeration) ist ein Vorschlag (`proposed`).

1. **E-Mail-Bestätigung: A1 mit MailKit. Entschieden (accepted).** `RequireConfirmedEmail = true`, eigener `IEmailSender<IdentityUser>` in `CleanArchitecture.Infrastructure` auf Basis von **MailKit** mit beliebigem SMTP-Server; MailKit als neue Abhängigkeit ist akzeptiert. Zugang, Port, TLS-Modus und Absenderadresse nur über Umgebungsvariablen/Secrets (z. B. `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__User`, `Email__Smtp__Password`, `Email__Smtp__Tls`, `Email__From`; genaue Schlüsselnamen legt Phase 1 fest), nichts davon im Code. Lokal fängt Papercut oder MailPit im Aspire-AppHost die Mails ab (Ressource: Phase 1 prüft, ob eine fertige Aspire-Integration existiert, sonst `AddContainer`). Begründung: Der Erweiterungspunkt existiert, `register` und `resendConfirmationEmail` rufen ihn auf, und `login` ist bis zur Bestätigung gesperrt (geprüft).
2. **MFA: freiwillig, TOTP zuerst. Entschieden (accepted).** B1 (TOTP + Recovery-Codes über `manage/2fa`) ist mit den vorhandenen Endpunkten ohne Server-Eigencode umsetzbar (geprüft). MFA ist freiwillig; eine Pflicht lässt sich später per Konfiguration nachziehen und wird jetzt nicht umgesetzt.
3. **Bestandskonten: per Migration als bestätigt markieren. Entschieden (accepted).** Eine neue EF-Migration setzt `EmailConfirmed = true` für alle vorhandenen Konten; bestehende Migrationen bleiben unverändert (CLAUDE.md). Nur neue Registrierungen brauchen den Bestätigungslink.
4. **Passkeys: erst Web, MAUI später in eigener Phase. Entschieden (accepted) für die Reihenfolge.** Phase 3 deckt Server und Blazor (WebAuthn per JS-Interop) ab, MAUI folgt in einer eigenen Phase 4. Server: eigene Endpunkte auf Basis der `SignInManager`/`UserManager`-Passkey-Methoden (vorhanden, geprüft). Relying-Party-ID und MAUI-Mindestversionen sind noch offen und werden in diesen Phasen geklärt; ob C1 oder C3 für MAUI gilt, entscheidet der Spike per Nachtrag zu diesem ADR.
5. **Enumeration (Vorschlag, `proposed`):** `register` bei vorhandener Adresse wird zu einer generischen Antwort (immer 200/202), der Besitzer des Kontos erhält eine Info-Mail („Konto existiert bereits“); `login` liefert für unbekannt, falsches Passwort und unbestätigt dieselbe Antwort. Das erfordert einen Filter oder eigene Endpunkte um `MapIdentityApi` (geprüft: das Standardverhalten verrät beides, siehe Fakten 5). Begründung: R-07 nennt die Enumeration ausdrücklich, und `RequireConfirmedEmail` allein würde ein zusätzliches Orakel (`NotAllowed`) schaffen.
4. **Enumeration:** `register` bei vorhandener Adresse wird zu einer generischen Antwort (immer 200/202), der Besitzer des Kontos erhält eine Info-Mail („Konto existiert bereits“); `login` liefert für unbekannt, falsches Passwort und unbestätigt dieselbe Antwort. Das erfordert einen Filter oder eigene Endpunkte um `MapIdentityApi` (geprüft: das Standardverhalten verrät beides, siehe Fakten 5). Begründung: R-07 nennt die Enumeration ausdrücklich, und `RequireConfirmedEmail` allein würde ein zusätzliches Orakel (`NotAllowed`) schaffen.

## Konsequenzen

Positiv:
- Konten sind an ein kontrolliertes Postfach gebunden; zweiter Faktor und phishing-resistenter Passkey verfügbar.
- Überwiegend Identity-Bordmittel; Passkey-Serverlogik muss nicht selbst kryptografisch geprüft werden (Identity-Kern prüft Attestation und Assertion).

Negativ und zu beachten:
- **Bestehende Konten:** Sie haben `EmailConfirmed = false` (ausgeführt wurde nur der Neukonto-Fall; der Wert bei Altkonten ist aus dem Identity-Standard abgeleitet, nicht in der Produktions-DB geprüft). Mit `RequireConfirmedEmail` wären sie ausgesperrt. Siehe Offene Fragen und Phase 1 (Migration/Bestätigungsmail).
- **Client-Anpassungen:** `AuthSession.RegisterAsync` (Auto-Login) und die entsprechende MAUI-Logik müssen auf den Zwischenzustand umgestellt werden; neue Texte DE/EN (`.resx`), ARIA für neue Formulare und Fehlermeldungen.
- **Rate Limit:** Das vorhandene Limit je IP (Standard 10/min) gilt für alle `/api/auth/*`, also auch für `confirmEmail`/`resendConfirmationEmail`; ein zusätzliches Limit je Zieladresse ist nötig, damit die API nicht als Mail-Bombe dient. Lockout (5 Fehlversuche/5 min) gilt bereits.
- **Tests:** `TestAuthHandler` umgeht die Anmeldung und bleibt für Fachtests. Zusätzlich braucht es Integrationstests mit echter Identity-Anmeldung (Fake-`IEmailSender`, wie im Wegwerfprojekt), je Scenario (Registrierung, Login vor/nach Bestätigung, Duplikat, Resend, 2FA, Recovery-Code, Passkey-Optionen). Im Repo existieren dafür keine Beispiele.
- **Altdaten-Übernahme (R-06):** `LegacyDataClaimer` darf erst nach `confirmEmail` laufen, nicht beim `register`-Filter, sonst übernimmt eine unbestätigte Adresse die Altdaten. Das ändert den Filter in `Program.cs`.
- **Recovery/Wiederherstellung:** Mit TOTP und Passkey als einzigen Faktoren braucht es Recovery-Codes (vorhanden) und einen definierten Weg bei Verlust aller Faktoren (Offene Fragen); `forgotPassword`/`resetPassword` setzen nur das Passwort zurück und sollten den zweiten Faktor nicht umgehen (Verhalten nicht geprüft).
- **Betrieb:** SMTP-Zugang und Absenderdomain (SPF/DKIM) sind Betriebsaufgaben (devops-agent); `docs/security/` und README listen die neuen Variablen.

## Umsetzungsschritte

Jede Phase ist ein eigener Change (`UserStories/*.openspec.md`, Scenarios als Tests), eigener Branch und PR.

**Phase 0 – Klärung und Spike (architect-agent, sec-agent):** Offene Fragen beantworten; Spike zu Passkey-Serverbausteinen (Konfiguration von `IdentityPasskeyOptions`, `IPasskeyHandler`-Registrierung, EF-Schema) und zu MAUI-Passkey-Machbarkeit (Binding oder Plattformcode, Mindestversionen).

**Phase 1 – E-Mail-Bestätigung (backend-agent, frontend-agent, devops-agent):**
1. Tests zuerst: Register → keine Anmeldung vor Bestätigung; `confirmEmail` → Anmeldung möglich; `resendConfirmationEmail` identische Antwort für bekannt/unbekannt; Duplikat- und Login-Antworten ohne Orakel; Altdaten erst nach Bestätigung; Rate-Limit je Adresse.
2. `IEmailSender<IdentityUser>` mit MailKit in Infrastructure, Optionen (`Email__Smtp__*`, `Email__From`) nur per Umgebungsvariablen/Secrets, Papercut/MailPit im AppHost, Variablen in README.
3. Neue EF-Migration: `EmailConfirmed = true` für alle vorhandenen Konten (entschieden); bestehende Migrationen nicht ändern. Test: Altkonto kann sich nach der Migration anmelden. Erst danach `RequireConfirmedEmail = true`.
4. Enumerations-Filter (Vorschlag); `LegacyDataClaimer`-Aufruf nach `confirmEmail` verlegen (Umfang abhängig von Frage 7).
5. Blazor und MAUI: Zwischenzustand „Bitte E-Mail bestätigen“, erneut senden; i18n DE/EN, ARIA. Ziel des Bestätigungslinks ist offen (Frage 8); Vorschlag für Phase 1: Blazor-Seite `/confirm-email`, die `confirmEmail` aufruft. Vorbehaltlich der Entscheidung.

**Phase 2 – TOTP-MFA mit Recovery-Codes (backend-agent, frontend-agent, ux-agent):**
1. Tests: Aktivieren mit gültigem/ungültigem Code, Login mit `twoFactorCode`, Login mit Recovery-Code (Code verbraucht), `RequiresTwoFactor`-Antwort, Verhalten von Refresh nach Aktivierung/Deaktivierung.
2. Contracts: `LoginRequest` um Codefelder, `manage/2fa`-Client in `IBloodPressureApi`/Auth-Client.
3. UI und MAUI: Einrichtung (Schlüssel, `otpauth://`-QR, Code bestätigen), Anzeige der Recovery-Codes einmalig, Anmeldedialog mit Code/Recovery-Code, Deaktivieren/Neu erzeugen.
4. MFA ist freiwillig (entschieden). Eine Pflicht-Konfiguration wird nicht umgesetzt, nur als spätere Option vorgemerkt.

**Phase 3 – Passkeys im Web (backend-agent, frontend-agent, sec-agent):** Voraussetzung: Relying-Party-ID geklärt (Frage 3).
1. Migration für `AspNetUserPasskeys` (neue Migration, keine Änderung bestehender).
2. Server: Endpunkte für Creation-Options, Registrierung, Request-Options und Anmeldung (Tokenausgabe wie `login`, Bearer/Refresh), `IdentityPasskeyOptions.ServerDomain`/`ValidateOrigin` aus Konfiguration; Verwaltung (Liste, Umbenennen/Entfernen); Tests mit simulierten Authenticator-Antworten.
3. Blazor: JS-Interop-Modul (Datei unter `wwwroot/js/`, CSP-konform ohne Inline-Skripte, siehe R-04/CSP-Dokument).
4. Abnahme in den gängigen Browsern; Dokumentation (doc-agent).

**Phase 4 – Passkeys in MAUI (eigene Phase, später; frontend-agent, sec-agent):** Mindestversionen und Relying-Party-ID werden hier geklärt (Frage 4 Rest, Frage 3). Inhalt: iOS (Associated Domains `webcredentials:`, `apple-app-site-association`), Android (`assetlinks.json`, Credential Manager), Hosting unter `/.well-known/` der Relying-Party-Domain, feste `ApplicationId` und Signatur-Fingerprints, Abnahme auf echten Geräten; Rückfall C3, falls C1 zu teuer.

## Risiken

| Risiko | Auswirkung | Gegenmaßnahme |
|---|---|---|
| Bestandskonten ausgesperrt | Nutzer verlieren Zugang zu ihren Daten | Phase-1-Migration oder Bestätigungsmail vor dem Umschalten; Rollout hinter Konfigurationsschalter |
| Fehlerhafte Mailzustellung (Spam, kein SPF/DKIM) | Neue Nutzer kommen nicht an | Absenderdomain vorbereiten, Resend-Pfad, Monitoring |
| Mail-Bombing/Missbrauch von `resend` und `register` | Reputationsschaden, Kosten | Rate-Limit je Adresse zusätzlich zu je IP, keine Orakel |
| Konto-Enumeration trotz Bestätigung | Datenschutz | Entscheidung 4, Tests auf identische Antworten und Laufzeiten (Timing nicht geprüft) |
| Verlust aller Faktoren (Telefon weg, Passkey weg) | Nutzer ausgesperrt oder Social Engineering beim Support | Recovery-Codes, definierter Wiederherstellungsweg, Mail-Bestätigung beim Zurücksetzen |
| Passkey-Aufwand in MAUI unterschätzt | Verzug | Spike in Phase 0; Rückfall C3; Web zuerst |
| Passkey-Domainbindung | Passkeys sind an die Relying-Party-ID gebunden; Domainwechsel macht sie ungültig | Domain vor Phase 3 festlegen |
| `LegacyDataClaimer` vor Bestätigung | Fremde Adresse übernimmt Altdaten | Aufruf nach `confirmEmail` (Phase 1), R-06-Maßnahmen |
| Refresh-Token umgeht neuen Faktor | Nach Aktivierung bleiben alte Tokens gültig | Verhalten prüfen; `UpdateSecurityStampAsync` bei Aktivierung/Deaktivierung vorsehen (Logout nutzt dies bereits) |
| Fremdabhängigkeit MailKit | Updates, CVEs | Dependabot für NuGet vorhanden |
| Aussagen zu iOS/Android nicht verifiziert | Falsche Planung | Spike, Mindestversionen dort belegen |

## Alternativen (verworfen)

- Nur Einladungscode statt Self-Service (A4): entspricht nicht dem Wunsch des Users, kann aber als zusätzliche Registrierungsbremse später ergänzt werden.
- Externer Identity-Provider (z. B. Keycloak, Entra ID): würde Passkeys und MFA mitbringen, ersetzt aber ADR 0001 und ist ein eigener Architekturwechsel; nicht Teil dieser Entscheidung.

## Offene Fragen an den User

1. **SMTP: Entschieden.** MailKit mit beliebigem SMTP, Zugang/Port/TLS/Absender nur über Umgebungsvariablen/Secrets, lokal Papercut/MailPit im AppHost; MailKit akzeptiert. (Der konkrete Produktiv-Anbieter und die Absenderdomain mit SPF/DKIM sind Betriebsdaten, die erst vor dem Produktivgang nötig sind.)
2. **MFA: Entschieden.** Freiwillig, TOTP zuerst mit Recovery-Codes; Pflicht später per Konfiguration.
3. **Passkeys, Relying-Party-ID: Offen.** Welche Domain ist die Relying-Party-ID (Produktiv und Test)? Läuft das Blazor-UI unter derselben Domain wie die API? Ist die Domain dauerhaft? Nötig vor Phase 3.
4. **Passkeys, Reihenfolge: Entschieden** (erst Web, MAUI später in Phase 4). **Offen:** Mindestversionen iOS/Android für MAUI (aktuell iOS 15.0 / Android 21.0 im Projekt) und ob C3 (Browser-Hop) als Zwischenlösung erlaubt ist; Klärung in Phase 4.
5. **Bestandskonten: Entschieden.** Per neuer EF-Migration `EmailConfirmed = true`; nur neue Registrierungen brauchen den Link.
6. **Wiederherstellung: Offen.** Wie kommt ein Nutzer zurück, der alle Faktoren verloren hat (nur Recovery-Codes, Mail-Link, Support)?
7. **Registrierung: Offen.** Bleibt sie offen, oder begrenzt zusätzlich ein Einladungscode (R-07/R-06) die Registrierung? Soll die Altdaten-Übernahme (R-06) per Konfigurationsschalter begrenzt werden?
8. **Bestätigungslink: Offen.** Soll er auf die API, eine Blazor-Seite oder einen Deep Link in die MAUI-App zeigen? Vorschlag für Phase 1: Blazor-Seite `/confirm-email`; Entscheidung steht aus.
