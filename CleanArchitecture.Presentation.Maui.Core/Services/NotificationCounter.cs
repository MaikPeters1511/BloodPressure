using CleanArchitecture.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Hält die Anzahl ungelesener Hinweise für die Anzeige am Tab.</summary>
public sealed class NotificationCounter(INotificationApi api, ILogger<NotificationCounter>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<NotificationCounter>.Instance;

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
            // Der Zähler ist nur eine Anzeige; ein Ausfall darf die App nicht stören.
            _logger.LogWarning(ex, "Ungelesene Hinweise konnten nicht ermittelt werden");
        }
    }

    public void Set(int unread)
    {
        Unread = unread;
        Changed?.Invoke();
    }
}
