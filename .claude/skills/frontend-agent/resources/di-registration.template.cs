using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Presentation.UI;

// Eine Extension-Methode je Schicht/Frontend. Aufruf: builder.Services.AddPresentationUiDI(builder.Configuration);
public static class DependencyInjection
{
    public static IServiceCollection AddPresentationUiDI(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<I[Name]ApiClient, [Name]ApiClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Api:BaseUrl"]
                ?? throw new InvalidOperationException("Api:BaseUrl fehlt in der Konfiguration."));
        });

        services.AddLocalization();

        return services;
    }
}

// MAUI-Variante (MauiProgram.CreateMauiApp):
//   builder.Services
//       .AddSingleton<I[Name]ApiClient, [Name]ApiClient>()
//       .AddTransient<[Name]ViewModel>()
//       .AddTransient<MainPage>();
