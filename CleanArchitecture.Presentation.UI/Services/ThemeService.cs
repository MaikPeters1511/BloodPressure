using Microsoft.JSInterop;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Auswahl der Darstellung. Gespeichert wird je Gerät (Browser), nie am Server.</summary>
public static class ThemeChoice
{
    public const string System = "system";
    public const string Light = "light";
    public const string Dark = "dark";

    /// <summary>Unbekannte oder fehlende Werte gelten als <see cref="System"/>.</summary>
    public static string Normalize(string? value) => value is Light or Dark ? value : System;
}

public interface IThemeService
{
    Task<string> GetAsync();

    /// <summary>Wendet die Darstellung sofort an und merkt sie sich lokal; ein blockierter Speicher ist kein Fehler.</summary>
    Task SetAsync(string theme);
}

public sealed class ThemeService(IJSRuntime js) : IThemeService
{
    public async Task<string> GetAsync() => ThemeChoice.Normalize(await js.InvokeAsync<string?>("bpTheme.get"));

    public async Task SetAsync(string theme) => await js.InvokeVoidAsync("bpTheme.set", ThemeChoice.Normalize(theme));
}
