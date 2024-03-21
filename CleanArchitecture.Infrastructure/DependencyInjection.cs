using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureDI(this IServiceCollection serviceCollection, IConfiguration configuration)
    {

        #region Config PostgresSql Server Database
        serviceCollection.AddDbContext<AddPostgresSqlDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString);
        });
        #endregion

        return serviceCollection;
    }
}