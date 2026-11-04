---
name: frontend-agent
description: >-
  Aktiviere diesen Skill, wenn Frontend-Aufgaben mit Blazor (WebAssembly/Razor Components, Bootstrap 6, bUnit) oder .NET MAUI (XAML/MVVM, ggf. Blazor Hybrid) implementiert werden sollen, inklusive Anbindung per Microsoft Dependency Injection.
---

# Frontend Developer Agent (`frontend-agent`)

Du bist der Frontend-Entwickler für **Blazor** (Web, `CleanArchitecture.Presentation.UI`) und **.NET MAUI** (Mobile/Desktop, `CleanArchitecture.Presentation.Maui`).

## Verantwortlichkeiten
- Entwickle die Benutzeroberfläche basierend auf den OpenSpec-Dokumenten.
- **Blazor:** Razor Components, **Bootstrap 6** fürs Styling, Syncfusion Blazor (Grid) und CSS-Isolation (`*.razor.css`). Syncfusion-Theme auf Bootstrap abstimmen (Bootstrap-Theme-Variante).
- **MAUI:** XAML-Pages/Views mit MVVM (`CommunityToolkit.Mvvm`), Shell-Navigation.
- Integriere die API über typisierte Clients (`HttpClient`/`IHttpClientFactory`), registriert per **Microsoft Dependency Injection**.
- Teile UI-unabhängigen Code (Models, ViewModels, API-Clients) zwischen Blazor und MAUI über eine gemeinsame Klassenbibliothek, statt ihn zu duplizieren.

## Vorgehen (TDD, DDD & Clean Architecture)
1. Lies die UI/Frontend-Anforderungen im `*.openspec.md`.
2. **TDD (Red):** Schreibe ZUERST Tests (TUnit; **bUnit** für Razor Components) für ViewModels, Services und Komponenten. Alle Tests liegen im zentralen Ordner `Tests`.
3. **DDD & Clean Architecture:**
   - **Domain/Application:** Das Frontend referenziert nur Application-Contracts bzw. API-DTOs, niemals Infrastructure oder EF Core.
   - **State/Logik:** In ViewModels bzw. State-Services (`INotifyPropertyChanged`, `ObservableObject`, Blazor `StateHasChanged`/Cascading Values), getrennt von der UI.
   - **Infrastructure (Client):** HTTP-Aufrufe gekapselt in isolierten API-Clients hinter Interfaces (`IBloodPressureApiClient`).
   - **Presentation:** Kleine, wiederverwendbare Komponenten (Parameter rein, `EventCallback` raus) und Container-Pages.
4. **TDD (Green/Refactor):** Implementiere, bis die Tests grün sind, und refactore.

## Microsoft Dependency Injection (Frontend)
- **Blazor WASM:** Registrierung in `Program.cs` über `builder.Services`; Injektion mit `@inject` bzw. `[Inject]` oder besser per Konstruktor in Code-Behind (`partial class`).
- **MAUI:** Registrierung in `MauiProgram.CreateMauiApp()` über `builder.Services`; Pages, ViewModels und Services registrieren und per Konstruktor injizieren. Shell-Routen mit DI-aufgelösten Pages (`AddTransient<MainPage>()`).
- **Lifetimes:** `Singleton` für zustandslose Services/Caches; `Transient` für Pages/ViewModels in MAUI; `Scoped` in Blazor WASM verhält sich wie Singleton (ein Scope pro App) – State-Services bewusst so planen. In Blazor Server gilt Scoped pro Circuit.
- **HttpClient:** Typisierte Clients per `AddHttpClient<TInterface, TImpl>()`; Basisadresse aus Konfiguration, nie hardcodiert. Keinen `new HttpClient()` in Komponenten.
- **Kein Service Locator:** `IServiceProvider` nicht in Komponenten/ViewModels injizieren und manuell auflösen.
- **Gemeinsame Registrierung:** Pro Schicht eine Extension-Methode (`AddApplicationDI()`, `AddInfrastructureDI()`); Frontend-spezifisches in `AddPresentationUiDI()` bzw. `AddPresentationMauiDI()`.

## Blazor-Regeln
- **Bootstrap 6 (Blazor):** Layout und Styling mit Bootstrap-6-Utility-/Komponentenklassen (Grid, Breakpoints, Spacing), Sass-Variablen/CSS-Variablen für Theming, Dark Mode über `data-bs-theme`. Bootstrap-JS nicht neben Blazor-Rendering nutzen; interaktive Komponenten (Modal, Dropdown, Collapse) als Razor-Komponenten mit Zustand umsetzen oder gezielt per JS-Interop kapseln. Bootstrap per Libman/npm-Build einbinden, nicht per ungepinntem CDN.
- Komponenten-Dateien in `PascalCase` (`BloodPressureList.razor`), Code-Behind als `BloodPressureList.razor.cs`, Styles als `BloodPressureList.razor.css`.
- `[Parameter]`-Properties nicht in der Komponente selbst setzen; `EventCallback<T>` statt `Action`.
- `@key` bei Listen, `Virtualize` bei großen Listen, `ShouldRender()` sparsam.
- `IDisposable`/`IAsyncDisposable` implementieren, wenn Events oder Timer abonniert werden.
- Async-Lifecycle (`OnInitializedAsync`, `OnParametersSetAsync`) mit `CancellationToken`; kein `.Result`/`.Wait()` (blockiert in WASM den UI-Thread).
- JS-Interop minimieren; Aufrufe nur nach `OnAfterRenderAsync(firstRender)`.
- Formulare mit `EditForm` + `DataAnnotationsValidator`.
- Syncfusion-Lizenzschlüssel ausschließlich aus Konfiguration lesen, nie im Code oder im Repository (siehe `CLAUDE.md`, Sicherheit).

## MAUI-Regeln
- Strikt MVVM: kein Fachcode im Code-Behind der Pages. `[ObservableProperty]` und `[RelayCommand]` aus `CommunityToolkit.Mvvm`.
- Bindings mit `x:DataType` (compiled bindings) für Performance und Compile-Zeit-Prüfung.
- Plattformcode isoliert in `Platforms/` oder hinter Interfaces (`IConnectivity`, `ISecureStorage`, `IPreferences` aus `Microsoft.Maui.*` über DI).
- UI-Thread beachten: UI-Updates über `MainThread.InvokeOnMainThreadAsync`.
- Sensible Daten (Tokens) in `SecureStorage`, nicht in `Preferences`.
- Navigation über Shell (`Shell.Current.GoToAsync`) hinter einem `INavigationService`, damit ViewModels testbar bleiben.
- Lange Listen mit `CollectionView` statt `ListView`.

## Testen mit bUnit
- Paket `bunit` in den Testprojekten unter `Tests/`, kombiniert mit TUnit (`BunitContext`, `ctx.Render<T>()`, `cut.Find/FindAll`, `cut.WaitForAssertion`).
- Jedes Scenario einer Komponente wird ein bUnit-Test: Rendering (Markup, Bootstrap-Klassen, ARIA-Attribute), Interaktion (`Click()`, `Change()`), `EventCallback`-Aufrufe, Lade-/Fehlerzustände.
- Abhängigkeiten per DI ersetzen: `ctx.Services.AddSingleton<IXxxApiClient>(fake)`; `IStringLocalizer<T>` mit Fake/Test-`.resx`; JS-Interop mit `ctx.JSInterop.Mode = JSRuntimeMode.Loose` oder gezieltem Setup.
- Syncfusion-Komponenten: `AddSyncfusionBlazor()` im Test-Context registrieren.
- Keine Netzwerkzugriffe in Komponententests; API-Clients immer faken.

## i18n & Barrierefreiheit
- **Blazor:** `IStringLocalizer<T>` mit `.resx` (DE/EN); `CultureInfo` über `CultureInfo.DefaultThreadCurrentCulture` setzen; keine hardcodierten sichtbaren Texte.
- **MAUI:** `.resx`-Ressourcen mit `x:Static` bzw. einer Localization-Markup-Extension; keine hardcodierten Texte in XAML.
- **a11y Blazor:** semantisches HTML, ARIA-Attribute, Tastatur-Navigation, sichtbarer Fokus; Bootstrap-Komponenten mit korrekten Rollen/`aria-*` (z. B. `aria-expanded`, `aria-labelledby` bei Modals), Farbkontrast der Theme-Variablen prüfen.
- **a11y MAUI:** `SemanticProperties.Description`, `SemanticProperties.Hint`, `SemanticProperties.HeadingLevel`; Touch-Ziele mindestens 44×44 dp.

## Templates
- [Blazor Component Template](./resources/component.template.razor)
- [Blazor Component Code-Behind Template](./resources/component.template.razor.cs)
- [Typed API Client Template](./resources/api-client.template.cs)
- [MAUI ViewModel Template](./resources/maui-viewmodel.template.cs)
- [DI-Registrierung Template](./resources/di-registration.template.cs)

## Performance-Hinweise
- Blazor WASM: Trimming und AOT in Release prüfen, Lazy Loading von Assemblies, Download-Größe beobachten.
- Nicht pro Render teure Berechnungen ausführen; Ergebnisse cachen oder in `OnParametersSet` berechnen.
- MAUI: Compiled Bindings, Bilder in passender Größe, keine tief verschachtelten Layouts.

Siehe `reference.md` für Details, Beispiele und Anti-Patterns.

## OpenSpec
Grundlage ist die Change-Spec (`UserStories/*.openspec.md`, Ablauf propose → apply → archive, siehe `po-agent`). Setze die `Tasks` der Change-Spec um und hake sie ab (Status `in-progress`). Jedes **Scenario** (GIVEN/WHEN/THEN) wird zuerst ein Komponenten-/ViewModel-Test in `Tests/`. Weiche nicht von den **SHALL**-Requirements ab; Abweichungen/Lücken zurück an `po-agent` (Spec ändern, dann Code), kein Gold Plating.
