using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace CleanArchitecture.Contracts;

public static class DependencyInjection
{
    /// <summary>
    /// Registriert <see cref="IBloodPressureApi"/> über <c>AddRefitClient</c>. Die Basisadresse kommt vom Aufrufer
    /// (aus der Konfiguration, unter Aspire z. B. <c>https+http://api</c> per Service-Discovery). Resilience und
    /// Service-Discovery liefern die <c>ConfigureHttpClientDefaults</c> aus den ServiceDefaults.
    /// Ist ein <see cref="IAccessTokenProvider"/> registriert, wird dessen Token als Bearer-Header gesendet.
    /// </summary>
    public static IHttpClientBuilder AddBloodPressureApiClient(this IServiceCollection services, Uri baseAddress)
    {
        services.AddTransient<BearerTokenHandler>();
        services.AddRefitClient<IAuthApi>().ConfigureHttpClient(c => c.BaseAddress = baseAddress);
        services.AddRefitClient<IReminderApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>();
        services.AddRefitClient<INotificationApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>();
        return services.AddRefitClient<IBloodPressureApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<BearerTokenHandler>();
    }
}
