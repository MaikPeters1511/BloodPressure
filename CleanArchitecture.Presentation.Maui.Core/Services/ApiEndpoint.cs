namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Prüft die konfigurierte API-Adresse: nie fest im Code, Klartext-HTTP nur in Debug-Builds.</summary>
public static class ApiEndpoint
{
    public static Uri Resolve(string? configured, bool allowCleartext)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Konfiguration 'Api:BaseUrl' fehlt (appsettings.json der App).");
        }

        if (!Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Konfiguration 'Api:BaseUrl' ist keine gültige http(s)-Adresse.");
        }

        if (uri.Scheme == "http" && !allowCleartext)
        {
            throw new InvalidOperationException("Konfiguration 'Api:BaseUrl' muss in Release-Builds HTTPS verwenden.");
        }

        return uri;
    }

    /// <summary>Im Android-Emulator erreicht man den Host-Rechner unter 10.0.2.2, nicht unter localhost.</summary>
    public static Uri ForAndroidEmulator(Uri uri)
        => uri.Host is "localhost" or "127.0.0.1" ? new UriBuilder(uri) { Host = "10.0.2.2" }.Uri : uri;
}
