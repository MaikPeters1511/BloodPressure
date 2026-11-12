using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Presentation.Maui.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;
    private readonly TwoFactorViewModel _twoFactor;

    public ProfilePage(ProfileViewModel viewModel, TwoFactorViewModel twoFactor)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        // Die Zwei-Faktor-Verwaltung hat einen eigenen Zustand und ist deshalb ein eigenes ViewModel.
        TwoFactorPanel.BindingContext = _twoFactor = twoFactor;
        twoFactor.FocusCodeRequested += () => Dispatcher.Dispatch(() => TwoFactorCodeEntry.Focus());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeCommand.Execute(null);
        _twoFactor.InitializeCommand.Execute(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Schlüssel und Wiederherstellungscodes nicht im Speicher halten, wenn die Seite verlassen wird.
        _twoFactor.CancelSetupCommand.Execute(null);
    }
}
