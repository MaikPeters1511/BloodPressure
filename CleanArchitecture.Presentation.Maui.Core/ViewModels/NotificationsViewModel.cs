using System.Collections.ObjectModel;
using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace CleanArchitecture.Presentation.Maui.Core.ViewModels;

/// <summary>Ein Hinweis, fertig lokalisiert; <see cref="Summary"/> ist die vollständige Screenreader-Ansage.</summary>
public sealed record NotificationItem(
    Guid Id, bool IsCritical, bool IsRead, string Title, string? CategoryText, CategoryLevel CategoryLevel,
    string Message, string DateText, string Summary)
{
    public bool HasCategory => CategoryText is not null;

    public bool IsUnread => !IsRead;
}

public sealed partial class NotificationsViewModel : ObservableObject
{
    private readonly INotificationApi _api;
    private readonly NotificationCounter _counter;
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _l;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    public NotificationsViewModel(
        INotificationApi api, NotificationCounter counter, SessionService session, INavigator navigator, IDialogService dialogs,
        ILocalizer localizer, TimeProvider time, ILogger<NotificationsViewModel>? logger = null)
    {
        _api = api;
        _counter = counter;
        _session = session;
        _navigator = navigator;
        _dialogs = dialogs;
        _l = localizer;
        _time = time;
        _logger = logger ?? NullLogger<NotificationsViewModel>.Instance;
    }

    public ObservableCollection<NotificationItem> Items { get; } = [];

    public bool IsEmpty => !IsBusy && Message is null && Items.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? _message;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await _navigator.GoToLoginAsync();
            return;
        }

        IsBusy = true;
        Message = null;
        try
        {
            var notifications = (await _api.GetAllAsync()).OrderByDescending(n => n.CreatedAt).ToList();
            Items.Clear();
            foreach (var notification in notifications)
            {
                Items.Add(ToItem(notification));
            }

            _counter.Set(notifications.Count(n => !n.IsRead));
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _session.LogoutAsync();
            await _navigator.GoToLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hinweise konnten nicht geladen werden");
            Message = _l["Error_Generic"];
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    [RelayCommand]
    private async Task MarkReadAsync(NotificationItem item)
    {
        Message = null;
        try
        {
            await _api.MarkAsReadAsync(item.Id);
            var index = Items.IndexOf(item);
            if (index >= 0)
            {
                Items[index] = Rebuild(item with { IsRead = true });
            }

            _counter.Set(Items.Count(i => !i.IsRead));
            _dialogs.Announce(_l["Notifications_MarkedRead"]);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _session.LogoutAsync();
            await _navigator.GoToLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hinweis konnte nicht als gelesen markiert werden");
            Message = _l["Error_Generic"];
        }
    }

    private NotificationItem ToItem(NotificationDto dto)
    {
        var critical = dto.Type == "CriticalReading";
        string? categoryText = null;
        var level = CategoryLevel.Unknown;
        if (dto.Category is { } category)
        {
            var name = _l[$"Category_{category}"];
            categoryText = name == $"Category_{category}" ? category : name;
            level = category switch
            {
                "NonElevated" => CategoryLevel.Normal,
                "Elevated" => CategoryLevel.Warning,
                                "Hypertension" => CategoryLevel.High,
                _ => CategoryLevel.Unknown
            };
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(dto.CreatedAt.ToUniversalTime(), _time.LocalTimeZone).ToString("g", _l.Culture);
        return Rebuild(new NotificationItem(
            dto.Id, critical, dto.IsRead, critical ? _l["Notifications_Warning"] : _l["Notifications_Reminder"],
            categoryText, level, LocalizedMessage(dto, categoryText), local, ""));
    }

    // Der Text kommt vom Server auf Deutsch; bekannte Typen werden in der App-Sprache dargestellt, unbekannte zeigen den Servertext.
    private string LocalizedMessage(NotificationDto dto, string? categoryText) => dto.Type switch
    {
        "CriticalReading" => _l.Format("Notification_CriticalText", categoryText ?? _l["Category_Hypertension"]),
        "MeasurementReminder" => _l["Notification_ReminderText"],
        _ => dto.Message
    };

    private NotificationItem Rebuild(NotificationItem item)
    {
        var status = item.IsRead ? _l["Notifications_Read"] : _l["Notifications_New"];
        var title = item.CategoryText is null ? item.Title : $"{item.Title} {item.CategoryText}";
        return item with { Summary = _l.Format("Notifications_Summary", title, status, item.Message, item.DateText) };
    }
}
