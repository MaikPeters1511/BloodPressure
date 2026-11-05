using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace CleanArchitecture.Presentation.Maui.Core.ViewModels;

public sealed partial class ProfileViewModel : ObservableObject
{
    private readonly IProfileApi _api;
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _l;
    private readonly ThemeSettings _theme;
    private readonly LanguageSettings _language;
    private readonly ILogger _logger;

    public ProfileViewModel(
        IProfileApi api, SessionService session, INavigator navigator, IDialogService dialogs,
        ILocalizer localizer, ThemeSettings theme, LanguageSettings language, ILogger<ProfileViewModel>? logger = null)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        _dialogs = dialogs;
        _l = localizer;
        _theme = theme;
        _language = language;
        _logger = logger ?? NullLogger<ProfileViewModel>.Instance;
        _selectedTheme = theme.Current;
        _selectedLanguage = language.Current;
    }

    public ObservableCollection<string> Errors { get; } = [];

    // --- Darstellung (je Gerät, lokal) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThemeSystem), nameof(IsThemeLight), nameof(IsThemeDark))]
    private string _selectedTheme;

    public bool IsThemeSystem { get => SelectedTheme == ThemeChoice.System; set => SetTheme(value, ThemeChoice.System); }

    public bool IsThemeLight { get => SelectedTheme == ThemeChoice.Light; set => SetTheme(value, ThemeChoice.Light); }

    public bool IsThemeDark { get => SelectedTheme == ThemeChoice.Dark; set => SetTheme(value, ThemeChoice.Dark); }

    private void SetTheme(bool selected, string theme)
    {
        // RadioButton meldet auch das Abwählen (false); nur die neue Auswahl zählt.
        if (!selected || SelectedTheme == theme)
        {
            return;
        }

        SelectedTheme = theme;
        _theme.Set(theme);
        _dialogs.Announce(_l.Format("Theme_Changed", _l[$"Theme_{char.ToUpperInvariant(theme[0])}{theme[1..]}"]));
    }

    // --- Sprache (je Gerät, lokal) ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLanguageSystem), nameof(IsLanguageGerman), nameof(IsLanguageEnglish))]
    private string _selectedLanguage;

    public bool IsLanguageSystem { get => SelectedLanguage == LanguageChoice.System; set => SetLanguage(value, LanguageChoice.System); }

    public bool IsLanguageGerman { get => SelectedLanguage == LanguageChoice.German; set => SetLanguage(value, LanguageChoice.German); }

    public bool IsLanguageEnglish { get => SelectedLanguage == LanguageChoice.English; set => SetLanguage(value, LanguageChoice.English); }

    private void SetLanguage(bool selected, string language)
    {
        // RadioButton meldet auch das Abwählen (false); nur die neue Auswahl zählt.
        if (!selected || SelectedLanguage == language)
        {
            return;
        }

        SelectedLanguage = language;
        _language.Set(language);

        // Die Texte sind jetzt in der neuen Sprache; die Ansage nennt die gewählte Sprache.
        var name = language switch
        {
            LanguageChoice.German => _l["Language_German"],
            LanguageChoice.English => _l["Language_English"],
            _ => _l["Language_System"]
        };
        _dialogs.Announce(_l.Format("Language_Changed", name));
    }

    // --- Profil (am Server) ---
    [ObservableProperty]
    private string _ageText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BmiText))]
    private string _heightText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BmiText))]
    private string _weightText = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public string Hint => _l["Profile_Hint"];

    /// <summary>BMI als Zahl; <c>null</c>, wenn eine Angabe fehlt oder ungültig ist. Keine Kategorie.</summary>
    public string? BmiText
        => TryParse(out var profile) && ProfileCalculator.Bmi(profile.HeightCm, profile.WeightKg) is { } bmi
            ? _l.Format("Profile_Bmi", bmi.ToString("0.0", _l.Culture))
            : null;

    public string BmiNote => _l["Profile_BmiNote"];

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await _navigator.GoToLoginAsync();
            return;
        }

        IsBusy = true;
        try
        {
            Show(await _api.GetAsync());
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await SignOutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profil konnte nicht geladen werden");
            Fail([_l["Error_Generic"]]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        StatusMessage = null;
        Errors.Clear();
        if (!TryParse(out var profile))
        {
            Fail([_l["Profile_Invalid"]]);
            return;
        }

        IsBusy = true;
        try
        {
            Show(await _api.PutAsync(profile));
            StatusMessage = _l["Profile_Saved"];
            _dialogs.Announce(StatusMessage);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await SignOutAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var messages = await ApiErrors.ValidationMessagesAsync(ex);
            Fail(messages.Count > 0 ? messages : [_l["Error_Generic"]]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profil konnte nicht gespeichert werden");
            Fail([_l["Error_Generic"]]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy || !await _dialogs.ConfirmAsync(_l["Profile_Delete"], _l["Profile_DeleteConfirm"], _l["Delete_Yes"], _l["Delete_Cancel"]))
        {
            return;
        }

        StatusMessage = null;
        Errors.Clear();
        try
        {
            await _api.DeleteAsync();
            Show(new ProfileDto(null, null, null));
            StatusMessage = _l["Profile_Deleted"];
            _dialogs.Announce(StatusMessage);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await SignOutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profil konnte nicht gelöscht werden");
            Fail([_l["Error_Generic"]]);
        }
    }

    private void Show(ProfileDto profile)
    {
        AgeText = profile.Age?.ToString(_l.Culture) ?? "";
        HeightText = profile.HeightCm?.ToString(_l.Culture) ?? "";
        WeightText = profile.WeightKg?.ToString("0.#", _l.Culture) ?? "";
    }

    // Leere Felder bedeuten "keine Angabe"; beim Gewicht werden Komma und Punkt als Dezimaltrenner akzeptiert.
    private bool TryParse(out ProfileDto profile)
    {
        profile = new ProfileDto(null, null, null);
        int? age = null, height = null;
        decimal? weight = null;

        if (!string.IsNullOrWhiteSpace(AgeText))
        {
            if (!int.TryParse(AgeText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var a)) return false;
            age = a;
        }

        if (!string.IsNullOrWhiteSpace(HeightText))
        {
            if (!int.TryParse(HeightText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var h)) return false;
            height = h;
        }

        if (!string.IsNullOrWhiteSpace(WeightText))
        {
            if (!decimal.TryParse(WeightText.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var w)) return false;
            weight = w;
        }

        profile = new ProfileDto(age, height, weight);
        return true;
    }

    private void Fail(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            Errors.Add(message);
        }

        _dialogs.Announce(string.Join(" ", Errors));
    }

    private async Task SignOutAsync()
    {
        await _session.LogoutAsync();
        await _navigator.GoToLoginAsync();
    }
}
