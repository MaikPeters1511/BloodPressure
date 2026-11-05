using System.Globalization;

namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Auswahl der Sprache. Gespeichert wird je Gerät, nie am Server.</summary>
public static class LanguageChoice
{
    public const string System = "system";
    public const string German = "de";
    public const string English = "en";

    /// <summary>Unbekannte oder fehlende Werte gelten als <see cref="System"/>.</summary>
    public static string Normalize(string? value) => value is German or English ? value : System;

    /// <summary>Die wirksame Sprache: bei "system" Englisch, wenn die Gerätesprache Englisch ist, sonst Deutsch.</summary>
    public static CultureInfo Resolve(string? choice, CultureInfo deviceCulture) => Normalize(choice) switch
    {
        English => new CultureInfo("en"),
        German => new CultureInfo("de"),
        _ => deviceCulture.TwoLetterISOLanguageName == "en" ? new CultureInfo("en") : new CultureInfo("de")
    };
}

/// <summary>Setzt die Sprache der laufenden App (Implementierung im MAUI-Projekt: Oberfläche neu aufbauen).</summary>
public interface ILanguageApplier
{
    void Apply(CultureInfo culture);
}

/// <summary>Spracheinstellung je Gerät. <paramref name="deviceCulture"/> ist die beim Start erkannte Gerätesprache (für "System").</summary>
public sealed class LanguageSettings(ISettingsStore store, ILocalizer localizer, ILanguageApplier applier, CultureInfo deviceCulture)
{
    public const string Key = "bp.language";

    public string Current => LanguageChoice.Normalize(store.Get(Key));

    /// <summary>Wirksame Kultur beim Start, abgeleitet aus gespeicherter Wahl und Gerätesprache.</summary>
    public static CultureInfo ResolveStored(ISettingsStore store, CultureInfo deviceCulture)
        => LanguageChoice.Resolve(store.Get(Key), deviceCulture);

    /// <summary>Wendet die Wahl sofort an und merkt sie sich; "system" entfernt den gespeicherten Wert.</summary>
    public void Set(string choice)
    {
        var normalized = LanguageChoice.Normalize(choice);
        store.Set(Key, normalized == LanguageChoice.System ? null : normalized);

        var culture = LanguageChoice.Resolve(normalized, deviceCulture);
        localizer.SetCulture(culture);
        applier.Apply(culture);
    }
}
