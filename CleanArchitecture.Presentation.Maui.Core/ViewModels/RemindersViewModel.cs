using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;

namespace CleanArchitecture.Presentation.Maui.Core.ViewModels;

/// <summary>Eine Erinnerungszeit; <see cref="Label"/> ist die Beschriftung für Screenreader ("Uhrzeit 2").</summary>
public sealed partial class ReminderTimeItem : ObservableObject
{
    [ObservableProperty]
    private TimeSpan _time;

    [ObservableProperty]
    private string _label = "";

    [ObservableProperty]
    private string _removeLabel = "";
}

public sealed partial class RemindersViewModel : ObservableObject
{
    public const int MaxTimes = 5;

    private readonly IReminderApi _api;
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _l;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    public RemindersViewModel(
        IReminderApi api, SessionService session, INavigator navigator, IDialogService dialogs,
        ILocalizer localizer, TimeProvider time, ILogger<RemindersViewModel>? logger = null)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        _dialogs = dialogs;
        _l = localizer;
        _time = time;
        _logger = logger ?? NullLogger<RemindersViewModel>.Instance;
    }

    public ObservableCollection<ReminderTimeItem> Times { get; } = [];

    public ObservableCollection<string> Errors { get; } = [];

    public string Hint => _l.Format("Reminders_Hint", MaxTimes);

    public bool CanAdd => Times.Count < MaxTimes;

    public bool HasNone => Times.Count == 0;

    [ObservableProperty]
    private string _timeZoneId = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await _navigator.GoToLoginAsync();
            return;
        }

        IsBusy = true;
        try
        {
            var settings = await _api.GetAsync();
            Times.Clear();
            foreach (var time in settings.Times)
            {
                if (TimeSpan.TryParseExact(time, "hh\\:mm", CultureInfo.InvariantCulture, out var parsed))
                {
                    Times.Add(new ReminderTimeItem { Time = parsed });
                }
            }

            // Ohne gespeicherte Erinnerungen schlägt die App die Zeitzone des Geräts vor.
            TimeZoneId = Times.Count == 0 ? _time.LocalTimeZone.Id : settings.TimeZoneId;
            Renumber();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await SignOutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erinnerungen konnten nicht geladen werden");
            Fail([_l["Error_Generic"]]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddTime()
    {
        if (!CanAdd)
        {
            return;
        }

        Times.Add(new ReminderTimeItem { Time = new TimeSpan(8, 0, 0) });
        Renumber();
    }

    [RelayCommand]
    private void RemoveTime(ReminderTimeItem item)
    {
        Times.Remove(item);
        Renumber();
        _dialogs.Announce(_l.Format("Reminders_Removed", item.Label));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        StatusMessage = null;
        Errors.Clear();
        var problems = Validate();
        if (problems.Count > 0)
        {
            Fail(problems);
            return;
        }

        IsBusy = true;
        try
        {
            var stored = await _api.PutAsync(new ReminderSettingsDto(
                TimeZoneId.Trim(), Times.Select(t => t.Time.ToString("hh\\:mm", CultureInfo.InvariantCulture)).ToList()));
            TimeZoneId = stored.TimeZoneId;
            StatusMessage = _l["Reminders_Saved"];
            _dialogs.Announce(StatusMessage);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await SignOutAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var messages = await ApiErrors.ValidationMessagesAsync(ex);
            Fail(messages.Count > 0 ? messages : [_l["Error_Generic"]]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erinnerungen konnten nicht gespeichert werden");
            Fail([_l["Error_Generic"]]);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private List<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(TimeZoneId))
        {
            problems.Add(_l["Reminders_ZoneMissing"]);
        }

        if (Times.Select(t => (int)t.Time.TotalMinutes).Distinct().Count() != Times.Count)
        {
            problems.Add(_l["Reminders_TimeDuplicate"]);
        }

        return problems;
    }

    private void Fail(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            Errors.Add(message);
        }

        _dialogs.Announce(string.Join(" ", Errors));
    }

    private void Renumber()
    {
        for (var i = 0; i < Times.Count; i++)
        {
            Times[i].Label = _l.Format("Reminders_TimeN", i + 1);
            Times[i].RemoveLabel = _l.Format("Reminders_Remove", i + 1);
        }

        OnPropertyChanged(nameof(CanAdd));
        OnPropertyChanged(nameof(HasNone));
    }

    private async Task SignOutAsync()
    {
        await _session.LogoutAsync();
        await _navigator.GoToLoginAsync();
    }
}
