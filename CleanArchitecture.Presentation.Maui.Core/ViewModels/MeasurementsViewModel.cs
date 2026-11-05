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

public sealed partial class MeasurementsViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly IBloodPressureApi _api;
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _l;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private int _page;
    private int _total;

    public MeasurementsViewModel(
        IBloodPressureApi api, SessionService session, INavigator navigator, IDialogService dialogs,
        ILocalizer localizer, TimeProvider time, ILogger<MeasurementsViewModel>? logger = null)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        _dialogs = dialogs;
        _l = localizer;
        _time = time;
        _logger = logger ?? NullLogger<MeasurementsViewModel>.Instance;
        var now = time.GetLocalNow();
        MeasuredDate = now.Date;
        MeasuredTime = now.TimeOfDay;
    }

    public ObservableCollection<MeasurementItem> Items { get; } = [];

    public ObservableCollection<string> FormErrors { get; } = [];

    public bool HasMore => Items.Count < _total;

    public bool IsEmpty => !IsBusy && Items.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleFormText))]
    private bool _isFormVisible;

    public string ToggleFormText => _l[IsFormVisible ? "Form_Hide" : "Form_Show"];

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string _systolicText = "";

    [ObservableProperty]
    private string _diastolicText = "";

    [ObservableProperty]
    private string _pulseText = "";

    [ObservableProperty]
    private string _oxygenText = "";

    [ObservableProperty]
    private DateTime _measuredDate;

    [ObservableProperty]
    private TimeSpan _measuredTime;

    [RelayCommand]
    private void ToggleForm() => IsFormVisible = !IsFormVisible;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await _navigator.GoToLoginAsync();
            return;
        }

        await LoadAsync(1);
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (HasMore && !IsBusy)
        {
            await LoadAsync(_page + 1);
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        FormErrors.Clear();
        if (!int.TryParse(SystolicText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var systolic)
            || !int.TryParse(DiastolicText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var diastolic)
            || !int.TryParse(PulseText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pulse)
            || !int.TryParse(OxygenText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oxygen))
        {
            ShowFormError(_l["Form_Missing"]);
            return;
        }

        // Eingabe in lokaler Zeit, an die API als UTC.
        var local = DateTime.SpecifyKind(MeasuredDate.Date + MeasuredTime, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, _time.LocalTimeZone);

        IsBusy = true;
        try
        {
            await _api.CreateAsync(new BloodPressureRequest(systolic, diastolic, utc, pulse, oxygen));
            SystolicText = DiastolicText = PulseText = OxygenText = "";
            Message = null;
            IsFormVisible = false;
            _dialogs.Announce(_l["M_Saved"]);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsBusy = false;
            await SignOutAsync();
            return;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var messages = await ApiErrors.ValidationMessagesAsync(ex);
            foreach (var message in messages.Count > 0 ? messages : [_l["Error_Generic"]])
            {
                FormErrors.Add(message);
            }

            _dialogs.Announce(string.Join(" ", FormErrors));
            IsBusy = false;
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Messwert konnte nicht gespeichert werden");
            ShowFormError(_l["Error_Generic"]);
            IsBusy = false;
            return;
        }

        IsBusy = false;
        await LoadAsync(1);
    }

    [RelayCommand]
    private async Task DeleteAsync(MeasurementItem item)
    {
        if (IsBusy || !await _dialogs.ConfirmAsync(_l["Delete_Title"], _l.Format("Delete_Confirm", item.DateText), _l["Delete_Yes"], _l["Delete_Cancel"]))
        {
            return;
        }

        Message = null;
        try
        {
            await _api.DeleteAsync(item.Id, item.ETag);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await SignOutAsync();
            return;
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            Message = _l["Conflict"];
            _dialogs.Announce(Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Messwert konnte nicht gelöscht werden");
            Message = _l["Error_Generic"];
            return;
        }

        await LoadAsync(1);
    }

    [RelayCommand]
    private async Task LogoutAsync() => await SignOutAsync();

    private async Task LoadAsync(int page)
    {
        IsBusy = true;
        try
        {
            var result = await _api.GetAllAsync(page: page, pageSize: PageSize, sort: BloodPressureListQuery.SortDateDescending);
            if (page == 1)
            {
                Items.Clear();
            }

            foreach (var dto in result.Items)
            {
                Items.Add(ToItem(dto));
            }

            _page = page;
            _total = result.TotalCount;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await SignOutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Messwerte konnten nicht geladen werden");
            Message = _l["Error_Generic"];
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasMore));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private async Task SignOutAsync()
    {
        await _session.LogoutAsync();
        await _navigator.GoToLoginAsync();
    }

    private void ShowFormError(string message)
    {
        FormErrors.Add(message);
        _dialogs.Announce(message);
    }

    private MeasurementItem ToItem(BloodPressureDto dto)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(dto.DateTime.ToUniversalTime(), _time.LocalTimeZone);
        var date = local.ToString("g", _l.Culture);
        var name = _l[$"Category_{dto.Category}"];
        var categoryText = name == $"Category_{dto.Category}" ? dto.Category : name;
        var level = dto.Category switch
        {
            "NonElevated" => CategoryLevel.Normal,
            "Elevated" => CategoryLevel.Warning,
            "Hypertension" => CategoryLevel.High,
            _ => CategoryLevel.Unknown
        };

        return new MeasurementItem(
            dto.Id, dto.Systolisch, dto.Diastolisch, dto.Pulse, dto.Oxygen, date, categoryText, level, dto.ETag,
            _l.Format("M_ItemSummary", date, dto.Systolisch, dto.Diastolisch, dto.Pulse, dto.Oxygen, categoryText));
    }
}
