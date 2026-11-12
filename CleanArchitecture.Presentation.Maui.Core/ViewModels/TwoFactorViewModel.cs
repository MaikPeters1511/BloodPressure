using System.Collections.ObjectModel;
using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace CleanArchitecture.Presentation.Maui.Core.ViewModels;

/// <summary>
/// Abschnitt „Zwei-Faktor-Anmeldung“ im Profil: einrichten, Wiederherstellungscodes erneuern, ausschalten.
/// Schlüssel, Codes und eingegebene Codes liegen nur hier im Speicher: nie loggen, nie ablegen, nach dem Schließen verwerfen.
/// </summary>
public sealed partial class TwoFactorViewModel : ObservableObject
{
    private enum PendingKind { None, Reset, Disable }

    private readonly IReminderApi _api;
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _l;
    private readonly ILauncherService _launcher;
    private readonly ILogger _logger;

    private string? _rawKey;
    private PendingKind _pending;

    public TwoFactorViewModel(
        IReminderApi api, SessionService session, INavigator navigator, IDialogService dialogs,
        ILocalizer localizer, ILauncherService launcher, ILogger<TwoFactorViewModel>? logger = null)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        _dialogs = dialogs;
        _l = localizer;
        _launcher = launcher;
        _logger = logger ?? NullLogger<TwoFactorViewModel>.Instance;
    }

    /// <summary>Die Seite soll den Fokus in das Code-Feld setzen.</summary>
    public event Action? FocusCodeRequested;

    public ObservableCollection<string> RecoveryCodes { get; } = [];

    // --- Texte ---
    public string Title => _l["TwoFactor_Title"];

    public string Hint => _l["TwoFactor_Hint"];

    public string OffText => _l["TwoFactor_Off"];

    public string SetupText => _l["TwoFactor_Setup"];

    public string SetupSteps => _l["TwoFactor_SetupSteps"];

    public string KeyLabel => _l["TwoFactor_Key"];

    public string CodeLabel => _l["TwoFactor_Code"];

    public string ConfirmText => _l["TwoFactor_Confirm"];

    public string CancelText => _l["Delete_Cancel"];

    public string OpenAppText => _l["TwoFactor_OpenApp"];

    public string ResetText => _l["TwoFactor_Reset"];

    public string DisableText => _l["TwoFactor_Disable"];

    public string RecoveryTitle => _l["TwoFactor_RecoveryTitle"];

    public string RecoveryHint => _l["TwoFactor_RecoveryHint"];

    public string RecoveryDoneText => _l["TwoFactor_RecoveryDone"];

    public string ReloginHint => _l["TwoFactor_Relogin"];

    public string ActionQuestion => _pending == PendingKind.Disable ? _l["TwoFactor_DisableConfirm"] : _l["TwoFactor_ResetConfirm"];

    public string StatusText => _l.Format("TwoFactor_StatusOn", CodesLeft);

    // --- Zustand ---
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOff), nameof(ShowOn), nameof(ShowSetup), nameof(ShowAction), nameof(ShowRecovery))]
    private bool _isLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOff), nameof(ShowOn))]
    private bool _isEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _codesLeft;

    /// <summary>Allgemeine Fehlermeldung (Laden fehlgeschlagen, keine Authenticator-App).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    /// <summary>Fehler am Code-Feld; der Fokus wird dorthin gesetzt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFieldError))]
    private string? _fieldError;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _codeText = "";

    /// <summary>Schlüssel in Vierergruppen; nur während der Einrichtung gesetzt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSetup), nameof(ShowOff), nameof(ShowOn))]
    private string? _setupKey;

    public bool HasError => ErrorMessage is not null;

    public bool HasFieldError => FieldError is not null;

    public bool HasRecoveryCodes => RecoveryCodes.Count > 0;

    public bool ShowRecovery => IsLoaded && HasRecoveryCodes;

    public bool ShowSetup => IsLoaded && SetupKey is not null && !HasRecoveryCodes;

    /// <summary>Das Feld für den 6-stelligen Code gehört zu Einrichten, Ausschalten und Neuerzeugen.</summary>
    public bool ShowCodeField => ShowSetup || ShowAction;

    public bool ShowAction => IsLoaded && _pending != PendingKind.None && !HasRecoveryCodes;

    public bool ShowOff => IsLoaded && !IsEnabled && SetupKey is null && !HasRecoveryCodes && !HasError;

    public bool ShowOn => IsLoaded && IsEnabled && SetupKey is null && _pending == PendingKind.None && !HasRecoveryCodes && !HasError;

    private void RefreshState()
    {
        OnPropertyChanged(nameof(HasRecoveryCodes));
        OnPropertyChanged(nameof(ShowRecovery));
        OnPropertyChanged(nameof(ShowSetup));
        OnPropertyChanged(nameof(ShowAction));
        OnPropertyChanged(nameof(ShowCodeField));
        OnPropertyChanged(nameof(ShowOff));
        OnPropertyChanged(nameof(ShowOn));
        OnPropertyChanged(nameof(ActionQuestion));
    }

    partial void OnErrorMessageChanged(string? value) => RefreshState();

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await _navigator.GoToLoginAsync();
            return;
        }

        await RunAsync(async () =>
        {
            Apply(await _api.ManageTwoFactorAsync(new TwoFactorRequest()));
            IsLoaded = true;
        }, onValidation: null);
    }

    [RelayCommand]
    private async Task StartSetupAsync()
    {
        StatusMessage = null;
        await RunAsync(async () =>
        {
            var response = await _api.ManageTwoFactorAsync(new TwoFactorRequest());
            Apply(response);
            if (response.IsTwoFactorEnabled)
            {
                return;
            }

            _rawKey = response.SharedKey;
            CodeText = "";
            FieldError = null;
            SetupKey = TotpSetup.FormatKey(response.SharedKey);
            RefreshState();
            FocusCodeRequested?.Invoke();
        }, onValidation: null);
    }

    [RelayCommand]
    private async Task OpenAuthenticatorAsync()
    {
        if (_rawKey is null)
        {
            return;
        }

        ErrorMessage = null;
        bool opened;
        try
        {
            opened = await _launcher.OpenAsync(new Uri(TotpSetup.BuildUri(TotpSetup.Issuer, _session.Email, _rawKey)));
        }
        catch (Exception ex)
        {
            // Nur die Ausnahme loggen, nie die Adresse (sie enthält den Schlüssel).
            _logger.LogWarning(ex, "Authenticator-App konnte nicht geöffnet werden");
            opened = false;
        }

        if (!opened)
        {
            ErrorMessage = _l["TwoFactor_OpenAppFailed"];
            _dialogs.Announce(ErrorMessage);
        }
    }

    [RelayCommand]
    private async Task ConfirmSetupAsync()
    {
        if (IsBusy || SetupKey is null)
        {
            return;
        }

        var code = NormalizeCode(CodeText);
        if (code is null)
        {
            Reject(_l["TwoFactor_CodeFormat"]);
            return;
        }

        await RunAsync(async () =>
        {
            var response = await _api.ManageTwoFactorAsync(new TwoFactorRequest(Enable: true, TwoFactorCode: code));
            Apply(response);
            ClearSecrets();
            ShowCodes(response.RecoveryCodes);
            StatusMessage = _l["TwoFactor_On"];
            _dialogs.Announce($"{StatusMessage} {ReloginHint}");
        }, onValidation: () => Reject(_l["TwoFactor_CodeInvalid"]));
    }

    [RelayCommand]
    private void CancelSetup() => ClearSecrets();

    [RelayCommand]
    private void BeginReset() => BeginAction(PendingKind.Reset);

    [RelayCommand]
    private void BeginDisable() => BeginAction(PendingKind.Disable);

    private void BeginAction(PendingKind kind)
    {
        StatusMessage = null;
        _pending = kind;
        CodeText = "";
        FieldError = null;
        RefreshState();
        FocusCodeRequested?.Invoke();
    }

    [RelayCommand]
    private void CancelAction() => ClearSecrets();

    // Ausschalten und Neuerzeugen verlangen den aktuellen Code aus der Authenticator-App.
    [RelayCommand]
    private async Task ConfirmActionAsync()
    {
        var kind = _pending;
        if (IsBusy || kind == PendingKind.None)
        {
            return;
        }

        var code = NormalizeCode(CodeText);
        if (code is null)
        {
            Reject(_l["TwoFactor_CodeFormat"]);
            return;
        }

        await RunAsync(async () =>
        {
            if (kind == PendingKind.Disable)
            {
                Apply(await _api.ManageTwoFactorAsync(new TwoFactorRequest(Enable: false, TwoFactorCode: code)));
                ClearSecrets();
                StatusMessage = $"{_l["TwoFactor_Off"]} {ReloginHint}";
                _dialogs.Announce(StatusMessage);
            }
            else
            {
                var response = await _api.ManageTwoFactorAsync(new TwoFactorRequest(ResetRecoveryCodes: true, TwoFactorCode: code));
                Apply(response);
                ClearSecrets();
                ShowCodes(response.RecoveryCodes);
                _dialogs.Announce(RecoveryTitle);
            }
        }, onValidation: () => Reject(_l["TwoFactor_CodeInvalid"]));
    }

    [RelayCommand]
    private void AcknowledgeCodes() => ClearSecrets();

    private void Apply(TwoFactorResponse response)
    {
        IsEnabled = response.IsTwoFactorEnabled;
        CodesLeft = response.RecoveryCodesLeft;
    }

    private void ShowCodes(string[]? codes)
    {
        foreach (var code in codes ?? [])
        {
            RecoveryCodes.Add(code);
        }

        RefreshState();
    }

    // Verwirft Schlüssel, eingegebenen Code und Wiederherstellungscodes aus dem Speicher.
    private void ClearSecrets()
    {
        _rawKey = null;
        SetupKey = null;
        _pending = PendingKind.None;
        RecoveryCodes.Clear();
        CodeText = "";
        FieldError = null;
        RefreshState();
    }

    private void Reject(string message)
    {
        FieldError = message;
        _dialogs.Announce(message);
        FocusCodeRequested?.Invoke();
    }

    // Sechs Ziffern; Leerzeichen (z. B. "123 456") werden entfernt.
    private static string? NormalizeCode(string? input)
    {
        var digits = new string((input ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray());
        return digits.Length == 6 && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    private async Task RunAsync(Func<Task> action, Action? onValidation)
    {
        IsBusy = true;
        FieldError = null;
        try
        {
            await action();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ClearSecrets();
            await _session.LogoutAsync();
            await _navigator.GoToLoginAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && onValidation is not null)
        {
            onValidation();
        }
        catch (Exception ex)
        {
            // Nur die Ausnahme ins Log (ohne Anfrage/Antwort), dem Nutzer eine saubere Meldung.
            _logger.LogError(ex, "Zwei-Faktor-Anfrage fehlgeschlagen");
            ClearSecrets();
            IsLoaded = true;
            ErrorMessage = _l["Error_Generic"];
            _dialogs.Announce(ErrorMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
