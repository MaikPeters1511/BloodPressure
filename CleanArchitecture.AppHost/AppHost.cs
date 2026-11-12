var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var database = postgres.AddDatabase("bloodpressuredb");

// Lokaler Mail-Server: fängt die Bestätigungsmails ab (Weboberfläche auf Port 8025, SMTP auf 1025). Nur für die Entwicklung.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.33.1")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "ui")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp");

builder.AddProject<Projects.CleanArchitecture_Presentation_API>("api")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(mailpit)
    .WithEnvironment("Email__Smtp__Host", mailpit.GetEndpoint("smtp").Property(EndpointProperty.Host))
    .WithEnvironment("Email__Smtp__Port", mailpit.GetEndpoint("smtp").Property(EndpointProperty.Port))
    .WithEnvironment("Email__Smtp__Tls", "None")
    .WithEnvironment("Email__From", "noreply@bloodpressure.local")
    .WithEnvironment("Email__ConfirmationBaseUrl", "http://localhost:5296/confirm-email")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.Build().Run();
