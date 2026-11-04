using Microsoft.JSInterop;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Zugriff auf Browserfunktionen (Dateidownload, Zeitzone), damit Seiten ohne JavaScript testbar bleiben.</summary>
public interface IBrowserInterop
{
    /// <summary>Lässt den Browser den Stream als Datei speichern.</summary>
    Task DownloadAsync(string fileName, Stream content);

    /// <summary>IANA-Zeitzone des Browsers, z. B. <c>Europe/Berlin</c>; <c>null</c>, wenn nicht ermittelbar.</summary>
    Task<string?> GetTimeZoneAsync();
}

public sealed class BrowserInterop(IJSRuntime js) : IBrowserInterop
{
    public async Task DownloadAsync(string fileName, Stream content)
    {
        using var reference = new DotNetStreamReference(content);
        await js.InvokeVoidAsync("bpDownload", fileName, reference);
    }

    public async Task<string?> GetTimeZoneAsync() => await js.InvokeAsync<string?>("getBrowserTimeZone");
}
