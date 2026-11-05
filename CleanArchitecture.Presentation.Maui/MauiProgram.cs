using System.Globalization;
using System.Reflection;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;
using CleanArchitecture.Presentation.Maui.Localization;
using CleanArchitecture.Presentation.Maui.Services;
using CleanArchitecture.Presentation.Maui.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Presentation.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
        const bool allowCleartext = true;
#else
        const bool allowCleartext = false;
#endif

        // Die API-Adresse kommt aus der eingebetteten Konfiguration, nie aus dem Code.
        var configuration = LoadConfiguration();
        var apiBase = ApiEndpoint.Resolve(configuration["Api:BaseUrl"], allowCleartext);
#if ANDROID
        apiBase = ApiEndpoint.ForAndroidEmulator(apiBase);
#endif

        var localizer = new ResxLocalizer(CultureInfo.CurrentUICulture);
        Loc.Localizer = localizer;

        var services = builder.Services;
        services.AddSingleton<ILocalizer>(localizer);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISecureStore, MauiSecureStore>();
        services.AddSingleton<INavigator, ShellNavigator>();
        services.AddSingleton<IDialogService, MauiDialogService>();
        services.AddSingleton<IShareService, MauiShareService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<NotificationCounter>();
        services.AddSingleton<AppShell>();
        services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<SessionService>());
        services.AddBloodPressureApiClient(apiBase);

        services.AddTransient<LoginViewModel>();
        services.AddTransient<MeasurementsViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<ExportViewModel>();
        services.AddTransient<RemindersViewModel>();
        services.AddTransient<NotificationsViewModel>();
        services.AddTransient<LoginPage>();
        services.AddTransient<MeasurementsPage>();
        services.AddTransient<StatisticsPage>();
        services.AddTransient<ExportPage>();
        services.AddTransient<RemindersPage>();
        services.AddTransient<NotificationsPage>();

        return builder.Build();
    }

    private static IConfiguration LoadConfiguration()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var configuration = new ConfigurationBuilder();
        foreach (var name in new[] { "appsettings.json", "appsettings.Debug.json" })
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            // Der Stream wird in den Speicher kopiert, weil der Builder ihn erst beim Build liest.
            var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            configuration.AddJsonStream(copy);
        }

        return configuration.Build();
    }
}
