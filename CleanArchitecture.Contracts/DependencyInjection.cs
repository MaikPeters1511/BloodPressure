using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace CleanArchitecture.Contracts;

public static class DependencyInjection
{
    /// <summary>
    /// Registriert <see cref="IBloodPressureApi"/> über <c>AddRefitClient</c>. Die Basisadresse kommt vom Aufrufer
    /// (aus der Konfiguration, unter Aspire z. B. <c>https+http://api</c> per Service-Discovery). Resilience und
    /// Service-Discovery liefern die <c>ConfigureHttpClientDefaults</c> aus den ServiceDefaults.
    /// </summary>
    public static IHttpClientBuilder AddBloodPressureApiClient(this IServiceCollection services, Uri baseAddress)
        => services.AddRefitClient<IBloodPressureApi>().ConfigureHttpClient(c => c.BaseAddress = baseAddress);
}
