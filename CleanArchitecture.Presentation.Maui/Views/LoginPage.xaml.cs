using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Presentation.Maui.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.FocusCodeRequested += () => Dispatcher.Dispatch(() =>
        {
            // Nur das sichtbare Code-Feld bekommt den Fokus (Bildschirmleser liest Beschriftung und Hinweis vor).
            (viewModel.ShowRecoveryCode ? RecoveryCodeEntry : AuthenticatorCodeEntry).Focus();
        });
    }
}
