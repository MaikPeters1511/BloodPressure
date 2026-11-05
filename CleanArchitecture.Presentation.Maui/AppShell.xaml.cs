using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Localization;

namespace CleanArchitecture.Presentation.Maui;

public partial class AppShell : Shell
{
    private readonly NotificationCounter _counter;
    private readonly SessionService _session;
    private readonly Action _onSessionChanged;

    public AppShell(NotificationCounter counter, SessionService session)
    {
        InitializeComponent();
        // Das Profil ist keine Tab-Seite, sondern wird über die Navigationsleiste der Messwerte geöffnet.
        Routing.RegisterRoute("profile", typeof(Views.ProfilePage));
        _counter = counter;
        _session = session;
        counter.Changed += UpdateNotificationsTitle;

        // Der Zähler wird nach jeder Anmeldung/Abmeldung neu bestimmt.
        _onSessionChanged = () => _ = session.IsAuthenticated ? counter.RefreshAsync() : Task.Run(() => counter.Set(0));
        session.Changed += _onSessionChanged;
        UpdateNotificationsTitle();
    }

    /// <summary>
    /// Löst die Ereignisse von Zähler und Sitzung. Nötig, wenn die Shell beim Sprachwechsel durch eine neue ersetzt wird
    /// (beide Dienste sind Singletons und würden sonst die alte Shell weiter bedienen).
    /// </summary>
    public void Detach()
    {
        _counter.Changed -= UpdateNotificationsTitle;
        _session.Changed -= _onSessionChanged;
    }

    // Der Tab-Titel trägt den Zähler (auch für Screenreader): "Hinweise (2)".
    private void UpdateNotificationsTitle()
    {
        var baseTitle = Loc.Localizer?["Tab_Notifications"] ?? "";
        var title = _counter.Unread > 0 ? $"{baseTitle} ({_counter.Unread})" : baseTitle;
        MainThread.BeginInvokeOnMainThread(() => NotificationsTab.Title = title);
    }
}
