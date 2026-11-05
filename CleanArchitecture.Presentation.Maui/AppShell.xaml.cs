using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Localization;

namespace CleanArchitecture.Presentation.Maui;

public partial class AppShell : Shell
{
    private readonly NotificationCounter _counter;

    public AppShell(NotificationCounter counter, SessionService session)
    {
        InitializeComponent();
        _counter = counter;
        counter.Changed += UpdateNotificationsTitle;

        // Der Zähler wird nach jeder Anmeldung/Abmeldung neu bestimmt.
        session.Changed += () => _ = session.IsAuthenticated ? counter.RefreshAsync() : Task.Run(() => counter.Set(0));
    }

    // Der Tab-Titel trägt den Zähler (auch für Screenreader): "Hinweise (2)".
    private void UpdateNotificationsTitle()
    {
        var baseTitle = Loc.Localizer?["Tab_Notifications"] ?? "";
        var title = _counter.Unread > 0 ? $"{baseTitle} ({_counter.Unread})" : baseTitle;
        MainThread.BeginInvokeOnMainThread(() => NotificationsTab.Title = title);
    }
}
