using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Presentation.Maui.Services;

/// <summary>Einstellungen je Gerät über <c>Preferences</c>.</summary>
public sealed class MauiSettingsStore : ISettingsStore
{
    public string? Get(string key) => Preferences.Default.ContainsKey(key) ? Preferences.Default.Get<string?>(key, null) : null;

    public void Set(string key, string? value)
    {
        if (value is null)
        {
            Preferences.Default.Remove(key);
        }
        else
        {
            Preferences.Default.Set(key, value);
        }
    }
}

/// <summary>Setzt <c>UserAppTheme</c>; "system" (<c>Unspecified</c>) folgt dem Gerät.</summary>
public sealed class MauiThemeApplier : IThemeApplier
{
    public void Apply(string theme)
    {
        var appTheme = theme switch
        {
            ThemeChoice.Light => AppTheme.Light,
            ThemeChoice.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current is { } app)
            {
                app.UserAppTheme = appTheme;
            }
        });
    }
}

/// <summary>
/// Wendet die Sprache an, indem die Shell (Tab-Titel und Seiten) in der neuen Sprache neu aufgebaut wird: <c>{loc:Translate}</c>
/// wird beim Erzeugen der Seite ausgewertet. Danach zeigt die App wieder die Profilseite; die Anmeldung bleibt erhalten.
/// </summary>
public sealed class MauiLanguageApplier(IServiceProvider services) : ILanguageApplier
{
    public void Apply(System.Globalization.CultureInfo culture)
        => MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (Application.Current?.Windows.FirstOrDefault() is not { } window)
            {
                return;
            }

            (window.Page as AppShell)?.Detach();
            window.Page = services.GetRequiredService<AppShell>();

            // Die neue Shell braucht einen Augenblick, bevor sie Navigation annimmt.
            await Task.Delay(100);
            if (Shell.Current is { } shell)
            {
                await shell.GoToAsync("//measurements");
                await shell.GoToAsync("profile");
            }
        });
}
