using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CleanArchitecture.Presentation.UI;


var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Syncfusion Api Key laden aus appsettings
var configuration = builder.Configuration;
var syncFusionLicenseKey = configuration.GetValue<string>("Syncfusion:LicenseKey");
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncFusionLicenseKey);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();