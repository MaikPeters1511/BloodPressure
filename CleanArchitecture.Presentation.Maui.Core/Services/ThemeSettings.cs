namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Auswahl der Darstellung. Gespeichert wird je Gerät, nie am Server.</summary>
public static class ThemeChoice
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";

    /// <summary>Unbekannte oder fehlende Werte gelten als <see cref="System"/>.</summary>
    public static string Normalize(string? value) => value is Light or Dark ? value : System;
}

/// <summary>Lokaler Einstellungsspeicher des Geräts (Implementierung im MAUI-Projekt über <c>Preferences</c>).</summary>
public interface ISettingsStore
{
    string? Get(string key);

    /// <summary>Speichert den Wert; <c>null</c> entfernt ihn.</summary>
    void Set(string key, string? value);
}

/// <summary>Setzt die Darstellung der laufenden App (Implementierung im MAUI-Projekt über <c>UserAppTheme</c>).</summary>
public interface IThemeApplier
{
    void Apply(string theme);
}

public sealed class ThemeSettings(ISettingsStore store, IThemeApplier applier)
{
    private const string Key = "bp.theme";

    public string Current => ThemeChoice.Normalize(store.Get(Key));

    /// <summary>Wendet die gespeicherte Wahl an (beim App-Start).</summary>
    public void ApplyStored() => applier.Apply(Current);

    /// <summary>Wendet die Wahl sofort an und merkt sie sich; "system" entfernt den gespeicherten Wert.</summary>
    public void Set(string theme)
    {
        var normalized = ThemeChoice.Normalize(theme);
        store.Set(Key, normalized == ThemeChoice.System ? null : normalized);
        applier.Apply(normalized);
    }
}
