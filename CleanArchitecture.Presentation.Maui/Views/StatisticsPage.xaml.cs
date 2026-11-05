using System.ComponentModel;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Presentation.Maui.Views;

public partial class StatisticsPage : ContentPage
{
    private readonly StatisticsViewModel _viewModel;
    private readonly ChartDrawable _drawable = new();

    public StatisticsPage(StatisticsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Chart.Drawable = _drawable;
        viewModel.PropertyChanged += OnViewModelChanged;
        Application.Current!.RequestedThemeChanged += (_, _) => Chart.Invalidate();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeCommand.Execute(null);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StatisticsViewModel.ChartSeries) or nameof(StatisticsViewModel.ChartLabels))
        {
            _drawable.Series = _viewModel.ChartSeries;
            _drawable.Labels = _viewModel.ChartLabels;
            Chart.Invalidate();
        }
    }
}
