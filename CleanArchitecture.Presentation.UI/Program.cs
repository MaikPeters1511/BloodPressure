using System.Globalization;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI;
using CleanArchitecture.Presentation.UI.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Syncfusion-Lizenz nur registrieren, wenn sie konfiguriert ist (nie im Repository ablegen).
var syncFusionLicenseKey = builder.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrWhiteSpace(syncFusionLicenseKey))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncFusionLicenseKey);
}

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddLocalization();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITokenStorage, SessionTokenStorage>();
builder.Services.AddSingleton<AuthSession>();
builder.Services.AddScoped<IBrowserInterop, BrowserInterop>();
builder.Services.AddSingleton<NotificationCounter>();
builder.Services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<AuthSession>());

// Die API-Adresse kommt aus wwwroot/appsettings.json (Api:BaseUrl), nie aus dem Code.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Konfiguration 'Api:BaseUrl' fehlt in wwwroot/appsettings.json.");
builder.Services.AddBloodPressureApiClient(new Uri(apiBaseUrl));

var host = builder.Build();

await host.Services.GetRequiredService<AuthSession>().InitializeAsync();

// Sprache aus dem Browser (Englisch, sonst Deutsch).
var browserLanguage = await host.Services.GetRequiredService<IJSRuntime>().InvokeAsync<string?>("getBrowserLanguage");
var culture = new CultureInfo(browserLanguage?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? "en" : "de");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

await host.RunAsync();
