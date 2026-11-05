namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Gibt eine Datei über den Teilen-Dialog des Betriebssystems aus (Implementierung im MAUI-Projekt).</summary>
public interface IShareService
{
    /// <summary>Speichert den Inhalt kurz in einer temporären Datei, öffnet den Teilen-Dialog und löscht die Datei danach.</summary>
    Task ShareFileAsync(string fileName, Stream content, string title);
}
