using CleanArchitecture.Contracts;
using Microsoft.Extensions.Logging;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Hält die Anzahl ungelesener Hinweise für die Navigation.</summary>
public sealed class NotificationCounter(INotificationApi api, ILogger<NotificationCounter> logger)
{
    public int Unread { get; private set; }

    public event Action? Changed;

    public async Task RefreshAsync()
    {
        try
        {
            Set((await api.GetAllAsync()).Count(n => !n.IsRead));
        }
        catch (Exception ex)
        {
            // Der Zähler ist nur eine Anzeige; ein Ausfall darf die Navigation nicht stören.
            logger.LogWarning(ex, "Ungelesene Hinweise konnten nicht ermittelt werden");
        }
    }

    public void Set(int unread)
    {
        Unread = unread;
        Changed?.Invoke();
    }
}
