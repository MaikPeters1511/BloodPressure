using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Presentation.Maui;

public partial class App : Application
{
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly AppShell _shell;

    public App(SessionService session, INavigator navigator, AppShell shell, ThemeSettings theme)
    {
        InitializeComponent();
        _session = session;
        _navigator = navigator;
        _shell = shell;
        // Gespeicherte Darstellung (hell/dunkel/System) vor dem ersten Fenster anwenden.
        theme.ApplyStored();
    }

    // Seit .NET 10 wird die Shell über CreateWindow gesetzt (MainPage.set ist veraltet).
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell);
        window.Created += async (_, _) =>
        {
            // Gespeicherte Anmeldung wiederherstellen; sonst bleibt die Anmeldeseite stehen.
            await _session.InitializeAsync();
            if (_session.IsAuthenticated)
            {
                await _navigator.GoToMeasurementsAsync();
            }
        };
        return window;
    }
}
