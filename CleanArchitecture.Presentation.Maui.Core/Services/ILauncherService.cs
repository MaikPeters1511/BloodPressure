namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Öffnet eine Adresse in der zuständigen App des Geräts (Implementierung im MAUI-Projekt über <c>Launcher</c>).</summary>
public interface ILauncherService
{
    /// <summary><c>true</c>, wenn eine App die Adresse geöffnet hat; <c>false</c>, wenn keine zuständig ist.</summary>
    Task<bool> OpenAsync(Uri uri);
}
