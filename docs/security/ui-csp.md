# Content-Security-Policy für das Blazor-UI

Bezug: `docs/security/owasp-epic-01.md`, Risiko R-04 (Tokens im `sessionStorage`, XSS-Folgeschaden).

## Stand

Das UI (`CleanArchitecture.Presentation.UI`) ist eine statische Blazor-WebAssembly-App. Dieses Repository enthält keinen Server, der sie ausliefert. Eine CSP muss deshalb vom Betreiber als **HTTP-Header** gesetzt werden. Ein `<meta>`-Tag reicht nicht (`frame-ancestors` wirkt dort nicht) und ist hier auch nicht möglich, weil die API-Adresse zur Laufzeit aus `wwwroot/appsettings.json` kommt.

`index.html` enthält keine Inline-Skripte mehr. Die Skripte liegen in `wwwroot/js/` (`theme.js`, `interop.js`, `boot.js`). Damit funktioniert eine CSP ohne `'unsafe-inline'` für Skripte.

## Empfohlener Header

```text
Content-Security-Policy: default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:; connect-src 'self' <API-ORIGIN>; object-src 'none'; base-uri 'self'; frame-ancestors 'none'
```

`<API-ORIGIN>` ist der Wert von `Api:BaseUrl` (z. B. `https://api.example.org`). Zusätzlich empfohlen: `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` und `Strict-Transport-Security` (HTTPS vorausgesetzt).

## Was geprüft wurde

Lokal im Browser mit dieser Policy (als `<meta>`-Tag, ohne `frame-ancestors`) und `Api:BaseUrl = http://127.0.0.1:5000`:

- Die Startseite lädt, Blazor startet, die Interop-Funktionen (`bpTheme`, `bpLang`, `bpDownload`, `getBrowserTimeZone`) sind vorhanden, die Konsole zeigt keine Verstöße außer dem Hinweis zu `frame-ancestors`.

Nicht geprüft: Seiten, die eine laufende API brauchen (Messwerte, Auswertung mit Diagramm, Export-Download über `blob:`). Ein Export-Download setzt einen `blob:`-Link per Skript; falls der Browser ihn blockiert, ist `blob:` bei `connect-src` oder `default-src` zu ergänzen. Vor dem Produktivbetrieb mit der echten API durchklicken.

## Offen

- Die Tokens bleiben im `sessionStorage` (`Services/SessionTokenStorage.cs`). Eine CSP verringert das XSS-Risiko, beseitigt es nicht. Langfristige Lösung laut Bericht: BFF mit HttpOnly-Cookie (ADR nötig).
