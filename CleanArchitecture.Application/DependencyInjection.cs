using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registriert Application-Services. Wolverine-Handler werden per Discovery gefunden
    /// (siehe <c>UseWolverine</c> im Host) und nicht manuell registriert.
    /// </summary>
    public static IServiceCollection AddApplicationDI(this IServiceCollection services) => services;
}
