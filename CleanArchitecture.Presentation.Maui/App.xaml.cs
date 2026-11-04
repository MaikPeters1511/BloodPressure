namespace CleanArchitecture.Presentation.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    // Seit .NET 10 wird die Shell über CreateWindow gesetzt (MainPage.set ist veraltet).
    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell());
}