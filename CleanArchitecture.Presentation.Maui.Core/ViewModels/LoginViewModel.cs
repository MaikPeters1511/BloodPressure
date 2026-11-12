using System.Net;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace CleanArchitecture.Presentation.Maui.Core.ViewModels;

public sealed partial class LoginViewModel(
    SessionService session, INavigator navigator, IDialogService dialogs, ILocalizer localizer, ILogger<LoginViewModel>? logger = null)
    : ObservableObject
{
    private readonly ILogger _logger = logger ?? NullLogger<LoginViewModel>.Instance;

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _noticeMessage;

    /// <summary>Zeigt den Hinweis zur Bestätigung samt „Erneut senden“ (nach Registrierung oder bei unbestätigtem Konto).</summary>
    [ObservableProperty]
    private bool _confirmationPending;

    [ObservableProperty]
    private bool _isBusy;

    public bool HasNotice => NoticeMessage is not null;

    private string _pendingEmail = "";

    // --- Zweiter Schritt (Zwei-Faktor-Anmeldung) ---
    // Das Passwort bleibt nur im Speicher dieses ViewModels, solange der zweite Schritt offen ist; es wird nie abgelegt.

    /// <summary>Die API hat einen Code verlangt: statt E-Mail und Passwort wird der Code abgefragt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCredentialsStep), nameof(ShowAuthenticatorCode), nameof(ShowRecoveryCode), nameof(CodeLabel), nameof(CodeHint), nameof(ToggleText))]
    private bool _requiresTwoFactor;

    /// <summary><c>true</c>: Wiederherstellungscode statt Code aus der Authenticator-App.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAuthenticatorCode), nameof(ShowRecoveryCode), nameof(CodeLabel), nameof(CodeHint), nameof(ToggleText))]
    private bool _useRecoveryCode;

    [ObservableProperty]
    private string _twoFactorCode = "";

    public bool IsCredentialsStep => !RequiresTwoFactor;

    public bool ShowAuthenticatorCode => RequiresTwoFactor && !UseRecoveryCode;

    public bool ShowRecoveryCode => RequiresTwoFactor && UseRecoveryCode;

    public string CodeLabel => localizer[UseRecoveryCode ? "Login_RecoveryCode" : "Login_TwoFactorCode"];

    public string CodeHint => localizer[UseRecoveryCode ? "Login_RecoveryHint" : "Login_TwoFactorHint"];

    public string ToggleText => localizer[UseRecoveryCode ? "Login_UseAuthenticator" : "Login_UseRecovery"];

    public string BackText => localizer["Login_Back"];

    /// <summary>Die Seite soll den Fokus in das Code-Feld setzen (Eingabehilfe, Tastatur).</summary>
    public event Action? FocusCodeRequested;

    [RelayCommand]
    private void ToggleRecoveryCode()
    {
        UseRecoveryCode = !UseRecoveryCode;
        TwoFactorCode = "";
        ErrorMessage = null;
        dialogs.Announce(CodeHint);
        FocusCodeRequested?.Invoke();
    }

    [RelayCommand]
    private void Back()
    {
        if (IsBusy)
        {
            return;
        }

        ResetTwoFactor();
        Password = "";
        ErrorMessage = null;
    }

    private void ResetTwoFactor()
    {
        RequiresTwoFactor = false;
        UseRecoveryCode = false;
        TwoFactorCode = "";
    }

    [RelayCommand]
    private Task LoginAsync() => SubmitAsync(async () =>
    {
        if (RequiresTwoFactor)
        {
            var code = UseRecoveryCode ? TwoFactorCode.Trim() : new string(TwoFactorCode.Where(char.IsLetterOrDigit).ToArray());
            if (code.Length == 0)
            {
                Fail(localizer["Login_CodeMissing"]);
                FocusCodeRequested?.Invoke();
                return;
            }

            await session.LoginAsync(Email.Trim(), Password,
                twoFactorCode: UseRecoveryCode ? null : code, recoveryCode: UseRecoveryCode ? code : null);
            ResetTwoFactor();
            Password = "";
            await navigator.GoToMeasurementsAsync();
            return;
        }

        await session.LoginAsync(Email.Trim(), Password);
        Password = "";
        await navigator.GoToMeasurementsAsync();
    }, registering: false);

    // Registrieren meldet nicht an: erst nach der Bestätigung der E-Mail-Adresse.
    [RelayCommand]
    private Task RegisterAsync() => SubmitAsync(async () =>
    {
        await session.RegisterAsync(Email.Trim(), Password);
        Password = "";
        ShowConfirmationNotice();
    }, registering: true);

    // Bewusst immer dieselbe Antwort, egal ob die Adresse existiert.
    [RelayCommand]
    private async Task ResendConfirmationAsync()
    {
        if (IsBusy || !ConfirmationPending)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await session.ResendConfirmationAsync(_pendingEmail);
            NoticeMessage = localizer["Login_Resent"];
            dialogs.Announce(NoticeMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bestätigungsmail konnte nicht erneut gesendet werden");
            Fail(localizer["Error_Generic"]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowConfirmationNotice()
    {
        _pendingEmail = Email.Trim();
        ConfirmationPending = true;
        NoticeMessage = localizer.Format("Login_ConfirmSent", _pendingEmail);
        dialogs.Announce(NoticeMessage);
    }

    private async Task SubmitAsync(Func<Task> action, bool registering)
    {
        if (IsBusy)
        {
            return;
        }

        if (!RequiresTwoFactor && (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password)))
        {
            Fail(localizer["Login_Missing"]);
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        NoticeMessage = null;
        ConfirmationPending = false;
        try
        {
            await action();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && registering)
        {
            var messages = await ApiErrors.ValidationMessagesAsync(ex);
            Fail(messages.Count > 0 ? string.Join(" ", messages) : localizer["Error_Generic"]);
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            if (ex.StatusCode == HttpStatusCode.Unauthorized && await ApiErrors.IsEmailNotConfirmedAsync(ex))
            {
                ShowConfirmationNotice();
            }
            else if (ex.StatusCode == HttpStatusCode.Unauthorized && await ApiErrors.IsTwoFactorRequiredAsync(ex))
            {
                UseRecoveryCode = false;
                TwoFactorCode = "";
                RequiresTwoFactor = true;
                dialogs.Announce(CodeHint);
                FocusCodeRequested?.Invoke();
            }
            else if (ex.StatusCode == HttpStatusCode.Unauthorized && await ApiErrors.IsLockedOutAsync(ex))
            {
                ResetTwoFactor();
                Password = "";
                Fail(localizer["Login_LockedOut"]);
            }
            else if (RequiresTwoFactor)
            {
                // Die Zugangsdaten stimmten schon: hier ist der Code falsch, nicht das Passwort.
                TwoFactorCode = "";
                Fail(localizer["Login_CodeInvalid"]);
                FocusCodeRequested?.Invoke();
            }
            else
            {
                Fail(localizer["Login_Failed"]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anmeldung fehlgeschlagen");
            Fail(localizer["Error_Generic"]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Fail(string message)
    {
        ErrorMessage = message;
        dialogs.Announce(message);
    }
}
