# Frontend-Referenz: Blazor, MAUI & Microsoft DI

Vertiefung zu `SKILL.md`. Projektbezug: `CleanArchitecture.Presentation.UI` (Blazor WebAssembly, Syncfusion) und `CleanArchitecture.Presentation.Maui`.

## 1. Microsoft Dependency Injection

### Lifetimes im Frontend

| Lifetime | Blazor WASM | MAUI | Einsatz |
|---|---|---|---|
| `Singleton` | eine Instanz pro App | eine Instanz pro App | zustandslose Services, Caches, `IBloodPressureApiClient`-Factory |
| `Scoped` | wie Singleton (ein Scope pro App); in Blazor Server pro Circuit | wie Singleton, außer bei manuell erzeugten Scopes | State-Services, die pro Benutzer-Sitzung leben |
| `Transient` | pro Auflösung neu | pro Auflösung neu | Pages, ViewModels (MAUI), leichtgewichtige Helfer |

### Captive Dependency vermeiden
Ein `Singleton` darf kein `Scoped`/`Transient` per Konstruktor halten, das kürzer leben soll. Aktiviere in Development die Validierung:

```csharp
// Blazor Server / ASP.NET Core
builder.Host.UseDefaultServiceProvider(o => { o.ValidateScopes = true; o.ValidateOnBuild = true; });
```

### Typisierter HttpClient

```csharp
builder.Services.AddHttpClient<IBloodPressureApiClient, BloodPressureApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Api:BaseUrl fehlt."));
});
```

Der Client ersetzt den bisherigen Singleton-`new HttpClient { BaseAddress = ... }` in `Program.cs`, damit Handler (Auth, Retry) über `AddHttpMessageHandler` einhängbar sind.

### Registrierung strukturieren
- Eine Extension-Methode je Schicht und Frontend: `AddApplicationDI()`, `AddInfrastructureDI(IConfiguration)`, `AddPresentationUiDI()`, `AddPresentationMauiDI()`.
- Interface und Implementierung registrieren, nie die konkrete Klasse injizieren.
- Mehrere Implementierungen: `IEnumerable<T>` injizieren oder Keyed Services (`AddKeyedSingleton`, `[FromKeyedServices("name")]`).
- Options-Pattern statt `IConfiguration` in Services: `services.Configure<ApiOptions>(config.GetSection("Api"))` und `IOptions<ApiOptions>`.

### Anti-Patterns

| Anti-Pattern | Besser |
|---|---|
| `IServiceProvider.GetService<T>()` in Komponenten/ViewModels (Service Locator) | Konstruktor- bzw. `@inject`-Injektion |
| `new HttpClient()` in Komponenten | typisierter Client per `AddHttpClient` |
| Scoped-Service im Singleton halten | Lifetime angleichen oder `IServiceScopeFactory` nutzen |
| Fachlogik im Konstruktor (I/O, `.Result`) | `OnInitializedAsync` bzw. Init-Command |
| Statische Singletons (`Instance`) statt DI | Registrierung im Container |
| Konfiguration/Secrets als Literale | Environment, `appsettings`, Secret Manager |

## 2. Blazor

### Komponentenmuster

```razor
@* BloodPressureList.razor *@
<ul aria-label="@Localizer["List.Label"]">
    @foreach (var item in Items)
    {
        <li @key="item.Id">
            <button type="button" @onclick="() => OnSelected.InvokeAsync(item)">
                @item.Systolic / @item.Diastolic
            </button>
        </li>
    }
</ul>
```

- Presentational Component: `[Parameter] IReadOnlyList<BloodPressureDto> Items`, `[Parameter] EventCallback<BloodPressureDto> OnSelected`.
- Container-Page: lädt Daten über den API-Client, hält Zustand, reicht Parameter nach unten.

### Rendermodi (ab .NET 8)
- **Reines WASM-Projekt (aktueller Stand):** alles läuft clientseitig, `Scoped` = App-Lebensdauer.
- **Blazor Web App:** pro Komponente `@rendermode InteractiveWebAssembly|InteractiveServer|InteractiveAuto`. Bei `Auto` müssen Services auf Server **und** Client registriert sein (gemeinsame Registrierung in einer geteilten Bibliothek).

### State
- Kleine Komponenten: Parameter + `EventCallback`.
- Seitenübergreifend: State-Service (`Scoped`) mit Event `OnChange`; Komponenten abonnieren in `OnInitialized` und lösen in `Dispose`.
- Tiefe Hierarchie: `CascadingValue` sparsam, bevorzugt `IsFixed="true"`.

### Formulare & Validierung
`EditForm` + `DataAnnotationsValidator` + `ValidationMessage`; Server-Fehler als `ValidationMessageStore` oder saubere Fehlermeldung anzeigen, nie Stacktraces (siehe `CLAUDE.md`).

### Bootstrap 6
- Einbindung über Libman/npm-Build in `wwwroot`, Version pinnen; Sass-Anpassungen in einer eigenen Datei statt Bootstrap-Quellen zu ändern.
- Layout: Grid (`container`, `row`, `col-*`), Utilities für Abstände/Ausrichtung; responsiv mobile-first.
- Interaktion: Modal, Dropdown, Collapse, Toast als Razor-Komponenten mit `bool`-Zustand und bedingten Klassen (`class="modal @(IsOpen ? "show d-block" : "")"`) statt Bootstrap-JS, damit Blazor den DOM besitzt. Fokus-Management und `aria-modal` selbst setzen.
- Theming: CSS-Variablen und `data-bs-theme="light|dark"`.
- Formulare: `form-control`, `form-select`, `is-invalid`/`invalid-feedback` zusammen mit `ValidationMessage`.
- Kein Mischen mit zweitem CSS-Framework.

### Syncfusion
- Lizenzschlüssel aus Konfiguration (`Syncfusion:LicenseKey`), `appsettings.sample.json` als Vorlage, echte Werte nicht einchecken.
- `SfGrid` mit `Virtualization`/Paging für große Datenmengen; Spalten-Header und Texte über Localizer.

### Tests mit bUnit

Setup: Paket `bunit` im Testprojekt unter `Tests/`. Komponenten über `BunitContext` rendern, Zustand mit `cut.WaitForAssertion` abwarten, Interaktion mit `Find(...).Click()`.

```csharp
[Test]
public async Task BloodPressureList_WithItems_RendersOneEntryPerItem()
{
    using var ctx = new BunitContext();
    var cut = ctx.Render<BloodPressureList>(p => p.Add(c => c.Items, items));
    await Assert.That(cut.FindAll("li").Count).IsEqualTo(items.Count);
}
```

- Services mit `ctx.Services.AddSingleton<IBloodPressureApiClient>(fake)` ersetzen.
- Localizer per Fake oder Test-`.resx` bereitstellen.

### Anti-Patterns

| Anti-Pattern | Besser |
|---|---|
| `.Result`/`.Wait()` in Lifecycle-Methoden | `await` in `OnInitializedAsync` |
| `async void` für Handler | `async Task` oder `EventCallback` |
| Parameter in der Komponente überschreiben | lokale Kopie oder `EventCallback` an den Parent |
| Event-Abonnement ohne `Dispose` | `IDisposable` implementieren |
| `StateHasChanged()` überall | nur nach Änderungen außerhalb des Blazor-Kontexts (`InvokeAsync(StateHasChanged)`) |
| `<div @onclick>` statt `<button>` | native Elemente, Tastatur- und Screenreader-tauglich |
| Bootstrap-JS manipuliert DOM, den Blazor rendert | Zustand in Razor, Klassen bedingt setzen |
| Hardcodierte Texte | `IStringLocalizer<T>` |
| Direkter DB-/DbContext-Zugriff | Zugriff nur über API-Client bzw. Application-Contract |

## 3. .NET MAUI

### Startpunkt (`MauiProgram.cs`)

```csharp
builder.Services
    .AddSingleton<IBloodPressureApiClient, BloodPressureApiClient>()
    .AddTransient<MainViewModel>()
    .AddTransient<MainPage>();
```

Page erhält das ViewModel per Konstruktor und setzt `BindingContext = viewModel;`.

### MVVM mit CommunityToolkit.Mvvm

```csharp
public partial class MainViewModel(IBloodPressureApiClient api) : ObservableObject
{
    [ObservableProperty] private bool _isBusy;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken ct) { /* ... */ }
}
```

- Private Felder `_camelCase`, generierte Properties `PascalCase` (siehe `CLAUDE.md`).
- Kein UI-Zugriff im ViewModel; Dialoge/Navigation über Interfaces.

### Plattform & Geräte
- Geräte-APIs über `Microsoft.Maui.*`-Interfaces per DI (`IConnectivity`, `IPreferences`, `ISecureStorage`).
- Bedingter Code mit `#if ANDROID` nur in `Platforms/` oder hinter Abstraktion.
- Offline-Verhalten: `Connectivity.Current.NetworkAccess` prüfen, Fehler nutzerfreundlich anzeigen.
- Emulator: Android erreicht den lokalen Host unter `10.0.2.2`, iOS-Simulator unter `localhost`; Basisadresse konfigurierbar halten.

### Blazor Hybrid (optional)
`BlazorWebView` in MAUI erlaubt, Razor-Komponenten aus `Presentation.UI` wiederzuverwenden. Dafür Komponenten in eine Razor Class Library auslagern und plattformspezifische Services (Storage, Navigation) hinter Interfaces halten. Entscheidung per ADR dokumentieren (`architect-agent`).

### Anti-Patterns

| Anti-Pattern | Besser |
|---|---|
| Logik im Page-Code-Behind | ViewModel + Command |
| `ListView` für große Listen | `CollectionView` |
| Bindings ohne `x:DataType` | Compiled Bindings |
| Tokens in `Preferences` | `SecureStorage` |
| UI-Update aus Hintergrund-Thread | `MainThread.InvokeOnMainThreadAsync` |
| Statische Zugriffe (`Shell.Current`) im ViewModel | `INavigationService` |
| Hardcodierte Texte/Farben | `.resx` und `ResourceDictionary` |

## 4. Performance-Ziele

| Metrik | Ziel |
|---|---|
| Blazor WASM Erst-Download (Release, komprimiert) | so klein wie möglich, Trimming aktiv |
| Interaktion (Klick bis UI-Update) | < 200 ms |
| MAUI Kaltstart | < 2 s auf Referenzgerät |
| Listen | virtualisiert (`Virtualize`, `CollectionView`) |
