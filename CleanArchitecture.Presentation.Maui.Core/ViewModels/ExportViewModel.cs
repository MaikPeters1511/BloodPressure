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

public sealed partial class ExportViewModel : ObservableObject
{
    private static readonly string[] Formats = [ExportQuery.Csv, ExportQuery.Pdf];

    private readonly IBloodPressureApi _api;
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly IShareService _share;
    private readonly ILocalizer _l;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    public ExportViewModel(
        IBloodPressureApi api, SessionService session, INavigator navigator, IDialogService dialogs, IShareService share,
        ILocalizer localizer, TimeProvider time, ILogger<ExportViewModel>? logger = null)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        _dialogs = dialogs;
        _share = share;
        _l = localizer;
        _time = time;
        _logger = logger ?? NullLogger<ExportViewModel>.Instance;
        var today = time.GetLocalNow().Date;
        _toDate = today;
        _fromDate = today.AddDays(-29);
    }

    public IReadOnlyList<string> FormatOptions { get; } = ["CSV", "PDF"];

    public string Hint => _l.Format("Export_Hint", ExportQuery.MaxMeasurements);

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _useRange;

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    private DateTime _toDate;

    [ObservableProperty]
    private int _selectedFormat;

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (!_session.IsAuthenticated)
        {
            await _navigator.GoToLoginAsync();
            return;
        }

        Message = StatusMessage = null;
        if (UseRange && FromDate.Date > ToDate.Date)
        {
            Message = _l["Range_Order"];
            _dialogs.Announce(Message);
            return;
        }

        var format = Formats[Math.Clamp(SelectedFormat, 0, Formats.Length - 1)];
        var (from, to) = UseRange ? DateRange.ToUtcBounds(FromDate, ToDate, _time.LocalTimeZone) : (null, null);
        var name = $"blutdruck-{(UseRange ? FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "alle")}-{(UseRange ? ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "alle")}.{format}";

        IsBusy = true;
        try
        {
            await using var content = await _api.ExportAsync(format, from, to);
            await _share.ShareFileAsync(name, content, _l["Export_Title"]);
            StatusMessage = _l.Format("Export_Done", name);
            _dialogs.Announce(StatusMessage);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _session.LogoutAsync();
            await _navigator.GoToLoginAsync();
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            Message = _l.Format("Export_TooMany", ExportQuery.MaxMeasurements);
            _dialogs.Announce(Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export fehlgeschlagen");
            Message = _l["Error_Generic"];
            _dialogs.Announce(Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
