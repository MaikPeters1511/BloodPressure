using System.Globalization;
using Microsoft.JSInterop;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Auswahl der Sprache. Gespeichert wird je Gerät (Browser), nie am Server.</summary>
public static class LanguageChoice
{
    public const string System = "system";
    public const string German = "de";
    public const string English = "en";

    /// <summary>Unbekannte oder fehlende Werte gelten als <see cref="System"/>.</summary>
    public static string Normalize(string? value) => value is German or English ? value : System;

    /// <summary>Die wirksame Sprache: bei "system" Englisch, wenn die Browsersprache mit "en" beginnt, sonst Deutsch.</summary>
    public static string Resolve(string? choice, string? browserLanguage) => Normalize(choice) switch
    {
        English => English,
        German => German,
        _ => browserLanguage?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true ? English : German
    };
}

public interface ILanguageService
{
    /// <summary>Wird nach jedem Sprachwechsel ausgelöst (Oberfläche neu zeichnen).</summary>
    event Action? Changed;

    /// <summary>Wirksame Sprache: <c>de</c> oder <c>en</c>.</summary>
    string Current { get; }

    /// <summary>Kultur der wirksamen Sprache für Texte und Formate. Sie ist unabhängig von der Kultur des Threads:
    /// Blazor WebAssembly legt diese beim Start fest, eine spätere Änderung wirkt dort nicht zuverlässig.</summary>
    CultureInfo Culture { get; }

    /// <summary>Gespeicherte Wahl (<c>system</c>, <c>de</c>, <c>en</c>).</summary>
    Task<string> GetChoiceAsync();

    /// <summary>Beim Start: gespeicherte Wahl bzw. Browsersprache anwenden, ohne <see cref="Changed"/> auszulösen.</summary>
    Task InitializeAsync();

    /// <summary>Wendet die Sprache sofort an und merkt sie sich lokal; ein blockierter Speicher ist kein Fehler.</summary>
    Task SetChoiceAsync(string choice);
}

public sealed class LanguageService(IJSRuntime js) : ILanguageService
{
    public event Action? Changed;

    public string Current { get; private set; } = LanguageChoice.German;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(Current);

    public async Task<string> GetChoiceAsync() => LanguageChoice.Normalize(await js.InvokeAsync<string?>("bpLang.get"));

    public async Task InitializeAsync() => await ApplyAsync(await GetChoiceAsync(), notify: false);

    public async Task SetChoiceAsync(string choice) => await ApplyAsync(LanguageChoice.Normalize(choice), notify: true);

    private async Task ApplyAsync(string choice, bool notify)
    {
        var browserLanguage = await js.InvokeAsync<string?>("getBrowserLanguage");
        Current = LanguageChoice.Resolve(choice, browserLanguage);

        // Texte und Formate der Oberfläche folgen Culture (siehe LanguageLocalizer); die Standardkultur wird für alles Übrige
        // (z. B. Meldungen des Frameworks) mitgeführt. Eine Zuweisung an CurrentCulture ginge in einer async-Methode verloren.
        var culture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Speichert die Wahl (bei "system" wird sie entfernt) und setzt das lang-Attribut der Seite.
        await js.InvokeVoidAsync("bpLang.set", choice, Current);

        if (notify)
        {
            Changed?.Invoke();
        }
    }
}

/// <summary>
/// Liefert die Texte aus <c>SharedResource(.en).resx</c> in der gewählten Sprache. Anders als der Standard-Lokalisierer liest er
/// die Sprache nicht von der Thread-Kultur, sondern vom <see cref="ILanguageService"/>, damit ein Wechsel sofort wirkt.
/// </summary>
public sealed class LanguageLocalizer(ILanguageService language) : Microsoft.Extensions.Localization.IStringLocalizer<SharedResource>
{
    private static readonly System.Resources.ResourceManager Resources =
        new("CleanArchitecture.Presentation.UI.SharedResource", typeof(SharedResource).Assembly);

    public Microsoft.Extensions.Localization.LocalizedString this[string name] => this[name, []];

    public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var culture = language.Culture;
            var value = Resources.GetString(name, culture);
            return new Microsoft.Extensions.Localization.LocalizedString(
                name, value is null ? name : string.Format(culture, value, arguments), resourceNotFound: value is null);
        }
    }

    public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}
