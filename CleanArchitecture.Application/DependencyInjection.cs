using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationDI(this IServiceCollection serviceCollection)
    {
        return serviceCollection;
    }
}