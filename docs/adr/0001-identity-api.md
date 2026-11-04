# ADR 0001: ASP.NET Core Identity API mit Bearer-Tokens

- **Status:** akzeptiert
- **Kontext:** Epic 1 verlangt Anmeldung und Benutzerbezug für Gesundheitsdaten. Clients sind Blazor WebAssembly und .NET MAUI (kein Cookie-Szenario).
- **Entscheidung:** ASP.NET Core Identity mit `AddIdentityApiEndpoints`/`MapIdentityApi` unter `/api/auth`, Benutzer in PostgreSQL (`IdentityDbContext`, gleicher Kontext wie die Fachdaten), Bearer- und Refresh-Tokens, Lockout nach 5 Fehlversuchen (5 Minuten).
- **Alternativen:** Externer IdP (Keycloak/Entra ID): mehr Betriebsaufwand und eigene Infrastruktur, für den aktuellen Umfang nicht nötig. Eigene JWT-Implementierung: unnötiges Sicherheitsrisiko.
- **Konsequenzen:**
  - Die Tokens sind undurchsichtige, von Identity verwaltete Bearer-Tokens (keine JWTs); Validierung nur durch diese API.
  - E-Mail-Bestätigung und Passwort-Reset sind ohne `IEmailSender` nicht nutzbar und bleiben Folgearbeit.
  - Ein späterer Wechsel auf einen externen IdP ist möglich, weil die `UserId` nur aus dem Claim `NameIdentifier` gelesen wird.
  - Altdaten werden dem ersten registrierten Nutzer zugeordnet.
