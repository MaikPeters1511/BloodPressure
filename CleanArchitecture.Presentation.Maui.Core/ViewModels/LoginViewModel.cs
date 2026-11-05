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
    private bool _isBusy;

    [RelayCommand]
    private Task LoginAsync() => SubmitAsync(() => session.LoginAsync(Email.Trim(), Password), registering: false);

    [RelayCommand]
    private Task RegisterAsync() => SubmitAsync(() => session.RegisterAsync(Email.Trim(), Password), registering: true);

    private async Task SubmitAsync(Func<Task> action, bool registering)
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password))
        {
            Fail(localizer["Login_Missing"]);
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
            Password = "";
            await navigator.GoToMeasurementsAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest && registering)
        {
            var messages = await ApiErrors.ValidationMessagesAsync(ex);
            Fail(messages.Count > 0 ? string.Join(" ", messages) : localizer["Error_Generic"]);
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            Fail(localizer["Login_Failed"]);
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
