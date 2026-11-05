using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Presentation.Maui.Views;

public partial class RemindersPage : ContentPage
{
    private readonly RemindersViewModel _viewModel;

    public RemindersPage(RemindersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeCommand.Execute(null);
    }
}
