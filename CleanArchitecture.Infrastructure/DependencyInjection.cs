using CleanArchitecture.Application;
using CleanArchitecture.Application.Export;
using CleanArchitecture.Infrastructure.Email;
using CleanArchitecture.Infrastructure.Export;
using CleanArchitecture.Domain.IRepository;
using CleanArchitecture.Infrastructure.Persistence.PostgreSql;
using CleanArchitecture.Infrastructure.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CleanArchitecture.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Name der Aspire-Datenbankressource bzw. des Connection Strings.</summary>
    public const string DatabaseName = "bloodpressuredb";

    /// <summary>
    /// Registriert DbContext (über die Aspire-Npgsql-Integration inkl. Health Check,
    /// Telemetrie und Retry) und die Repositories. Der Connection String kommt aus
    /// <c>ConnectionStrings:bloodpressuredb</c> (Aspire injiziert ihn per Umgebungsvariable).
    /// </summary>
    public static IHostApplicationBuilder AddInfrastructureDI(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AddPostgresSqlDbContext>(DatabaseName);
        builder.Services.AddScoped<IBloodPressureRepository, BloodPressureRepository>();
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<IReminderRepository, ReminderRepository>();
        builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        builder.Services.AddScoped<ILegacyDataClaimer, LegacyDataClaimer>();
        builder.Services.AddSingleton<IExportWriter, CsvExportWriter>();
        builder.Services.AddSingleton<IExportWriter, PdfExportWriter>();
        builder.AddEmailSending();

        return builder;
    }
}
