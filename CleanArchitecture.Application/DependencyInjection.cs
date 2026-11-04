using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CleanArchitecture.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registriert Validatoren und die Systemuhr. Wolverine-Handler werden per Discovery gefunden
    /// (siehe <c>UseWolverine</c> im Host) und nicht manuell registriert.
    /// </summary>
    public static IServiceCollection AddApplicationDI(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Reminders.ReminderProcessor>();
        services.AddValidatorsFromAssemblyContaining<ILegacyDataClaimer>(ServiceLifetime.Singleton);
        return services;
    }
}
