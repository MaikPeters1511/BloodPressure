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
