# Blazor & .NET MAUI Review-Checkliste

Bezug: `CLAUDE.md` (Frontend, i18n DE/EN, ARIA/Accessibility, TDD) und Skill `frontend-agent`.
Projekte: `CleanArchitecture.Presentation.UI` (Blazor WASM, Syncfusion), `CleanArchitecture.Presentation.Maui`.

## Microsoft Dependency Injection
- Registrierung im Composition Root (`Program.cs`, `MauiProgram.cs`) bzw. in Extension-Methoden (`AddXxxDI`); keine verstreuten Registrierungen.
- Konstruktor-/`@inject`-Injektion statt Service Locator (`IServiceProvider.GetService` in Komponenten/ViewModels)?
- Lifetimes passend: Blazor WASM `Scoped` ≈ `Singleton`; MAUI Pages/ViewModels `Transient`; kein Singleton, das Scoped/Transient hält (Captive Dependency).
- `new HttpClient()` in Komponenten statt typisiertem Client (`AddHttpClient<TInterface, TImpl>`)?
- Konfigurationswerte (Basis-URL, Lizenzschlüssel) per Options/Konfiguration, nicht hardcodiert.

## Bootstrap 6
- Bootstrap-JS verändert DOM, den Blazor rendert (Modal/Dropdown/Collapse)? Besser Zustand in Razor.
- Ungepinntes CDN oder doppelte Frameworks (zweites CSS-Framework)?
- Responsive Klassen (Grid/Breakpoints) und korrekte ARIA-Attribute an Bootstrap-Komponenten?
- Eigene Overrides über Sass/CSS-Variablen statt Änderungen an Bootstrap-Quellen?

## Blazor: Komponenten & Lifecycle
- Parameter nicht in der Komponente selbst überschrieben; `EventCallback` statt `Action`/`Func`?
- `.Result`/`.Wait()`/`async void` in Lifecycle-Methoden oder Handlern (blockiert in WASM den UI-Thread)?
- Abonnierte Events/Timer/`DotNetObjectReference` in `Dispose`/`DisposeAsync` freigegeben?
- `@key` bei dynamischen Listen, `Virtualize` bei großen Listen?
- `StateHasChanged()` nur dort, wo nötig (`InvokeAsync` bei Aufrufen außerhalb des Renderers)?
- Teure Berechnungen im Markup bei jedem Render statt in `OnParametersSet`/Cache?
- `EditForm` mit Validierung (`DataAnnotationsValidator`); Fehler als saubere Meldung, kein Stacktrace?
- JS-Interop nur nach `OnAfterRenderAsync(firstRender)`, Eingaben nicht ungeprüft in JS-Code?

## MAUI: MVVM & Plattform
- Fachlogik im Page-Code-Behind statt im ViewModel?
- `x:DataType` (Compiled Bindings) gesetzt?
- UI-Updates aus Hintergrund-Threads ohne `MainThread`?
- `CollectionView` statt `ListView` für lange Listen; Bildgrößen angemessen?
- Plattformcode (`#if ANDROID`, `Platforms/`) hinter Interfaces gekapselt?
- Tokens/Geheimnisse in `Preferences` statt `SecureStorage`?
- Navigation testbar (Interface statt `Shell.Current` im ViewModel)?

## i18n
- Sichtbare Strings hardcodiert in Razor/XAML/C# statt `IStringLocalizer<T>` bzw. `.resx` (`CLAUDE.md`: strikt DE/EN)? Betrifft auch Fehlermeldungen, Placeholder, ARIA-Labels und `SemanticProperties`.

## Barrierefreiheit (a11y/ARIA)
- Blazor: `<button>`/`<a>` statt `<div @onclick>`; Formularelemente mit `<label>`/`aria-label`; Fokus-Management bei Dialogen; sichtbarer Fokus; Farbkontrast.
- MAUI: `SemanticProperties.Description/Hint/HeadingLevel` gesetzt, Touch-Ziele ≥ 44×44 dp, Dynamic Type/Textskalierung nicht blockiert.
- Status nicht nur über Farbe vermittelt.

## Typsicherheit
- Nullable Reference Types konsistent; `!` nur mit echter Garantie.
- Serialisierte API-DTOs typisiert statt `dynamic`/`object`/`JsonElement` im UI-Code?

## Security (Frontend-spezifisch, ergänzt security-checklist.md)
- `MarkupString`/`@((MarkupString)…)` oder `innerHTML` mit ungeprüftem User-Input (XSS)?
- Tokens im `localStorage`/`sessionStorage` (Blazor) bzw. `Preferences` (MAUI) statt sicherer Ablage?
- `[Authorize]`/`AuthorizeView` im Frontend decken sich mit den serverseitigen Regeln (UI-Ausblenden ersetzt keine Backend-Autorisierung)?
- Syncfusion-Lizenzschlüssel oder andere Secrets im Repository (`appsettings*.json`)?
- CORS im Backend nicht mit `AllowAnyOrigin` produktiv.

## Tests
- Test im vorgeschriebenen `Tests`-Ordner (`CLAUDE.md`)?
- Razor-Komponenten mit bUnit getestet (Rendering, Interaktion, `EventCallback`, DI-Fakes), ViewModels ohne UI-Abhängigkeit testbar?
- TDD erkennbar (Test deckt das erwartete Verhalten ab, nicht nur den aktuellen Implementierungsstand)?
