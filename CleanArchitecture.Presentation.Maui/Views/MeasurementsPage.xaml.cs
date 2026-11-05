using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Presentation.Maui.Views;

public partial class MeasurementsPage : ContentPage
{
    private readonly MeasurementsViewModel _viewModel;

    public MeasurementsPage(MeasurementsViewModel viewModel)
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
