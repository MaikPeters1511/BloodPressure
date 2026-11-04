using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CleanArchitecture.Presentation.Maui.ViewModels;

// Registrierung: builder.Services.AddTransient<[Name]ViewModel>(); (siehe di-registration.template.cs)
public partial class [Name]ViewModel(I[Name]ApiClient apiClient) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private bool _isBusy;

    public ObservableCollection<[Name]Dto> Items { get; } = [];

    [RelayCommand(CanExecute = nameof(CanLoad))]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            Items.Clear();
            foreach (var item in await apiClient.GetAllAsync(cancellationToken))
            {
                Items.Add(item);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLoad() => !IsBusy;
}
