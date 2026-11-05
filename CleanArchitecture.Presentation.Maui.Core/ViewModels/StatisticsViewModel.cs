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

/// <summary>Linienart: Reihen unterscheiden sich nicht nur durch die Farbe (Barrierefreiheit).</summary>
public enum LineStyle { Solid, Dashed, Dotted }

public sealed record ChartSeriesData(string Name, IReadOnlyList<double> Values, LineStyle Style);

/// <summary>Eine Zeile der Wertetabelle; <see cref="Summary"/> ist die vollständige Screenreader-Ansage.</summary>
public sealed record StatisticsRow(string PeriodText, int Count, string Systolic, string Diastolic, string Pulse, string Summary);

public sealed partial class StatisticsViewModel : ObservableObject
{
    private static readonly string[] IntervalKeys = [StatisticsQuery.Day, StatisticsQuery.Week, StatisticsQuery.Month];

    private readonly IBloodPressureApi _api;
    private readonly SessionService _session;
    private readonly INavigator _navigator;
    private readonly IDialogService _dialogs;
    private readonly ILocalizer _l;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private string _shownInterval = StatisticsQuery.Day;

    public StatisticsViewModel(
        IBloodPressureApi api, SessionService session, INavigator navigator, IDialogService dialogs,
        ILocalizer localizer, TimeProvider time, ILogger<StatisticsViewModel>? logger = null)
    {
        _api = api;
        _session = session;
        _navigator = navigator;
        _dialogs = dialogs;
        _l = localizer;
        _time = time;
        _logger = logger ?? NullLogger<StatisticsViewModel>.Instance;
        var today = time.GetLocalNow().Date;
        _toDate = today;
        _fromDate = today.AddDays(-29);
        IntervalOptions = [_l["Interval_Day"], _l["Interval_Week"], _l["Interval_Month"]];
    }

    public IReadOnlyList<string> IntervalOptions { get; }

    public ObservableCollection<StatisticsRow> Rows { get; } = [];

    public IReadOnlyList<ChartSeriesData> ChartSeries { get; private set; } = [];

    public IReadOnlyList<string> ChartLabels { get; private set; } = [];

    public string ChartDescription => _l["Stats_ChartLabel"];

    public bool HasData => Rows.Count > 0;

    public bool IsEmpty => !IsBusy && Message is null && Rows.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string? _message;

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    private DateTime _toDate;

    [ObservableProperty]
    private int _selectedInterval;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!_session.IsAuthenticated)
        {
            await _navigator.GoToLoginAsync();
            return;
        }

        await ApplyAsync();
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (IsBusy)
        {
            return;
        }

        Message = null;
        if (FromDate.Date > ToDate.Date)
        {
            Message = _l["Range_Order"];
            _dialogs.Announce(Message);
            return;
        }

        var (from, to) = DateRange.ToUtcBounds(FromDate, ToDate, _time.LocalTimeZone);
        var interval = IntervalKeys[Math.Clamp(SelectedInterval, 0, IntervalKeys.Length - 1)];
        IsBusy = true;
        try
        {
            var buckets = await _api.GetStatisticsAsync(from, to, interval);
            _shownInterval = interval;
            Show(buckets);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            await _session.LogoutAsync();
            await _navigator.GoToLoginAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Statistik konnte nicht geladen werden");
            Show([]);
            Message = _l["Error_Generic"];
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(HasData));
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    private void Show(IReadOnlyList<StatisticsBucketDto> buckets)
    {
        Rows.Clear();
        foreach (var bucket in buckets)
        {
            var period = Period(bucket.PeriodStart);
            var systolic = Cell(bucket.Systolisch);
            var diastolic = Cell(bucket.Diastolisch);
            var pulse = Cell(bucket.Pulse);
            Rows.Add(new StatisticsRow(period, bucket.Count, systolic, diastolic, pulse,
                _l.Format("Stats_RowSummary", period, bucket.Count, systolic, diastolic, pulse)));
        }

        ChartLabels = buckets.Select(b => Period(b.PeriodStart)).ToList();
        ChartSeries = buckets.Count == 0
            ? []
            :
            [
                new(_l["Col_Systolic"], buckets.Select(b => b.Systolisch.Average).ToList(), LineStyle.Solid),
                new(_l["Col_Diastolic"], buckets.Select(b => b.Diastolisch.Average).ToList(), LineStyle.Dashed),
                new(_l["Col_Pulse"], buckets.Select(b => b.Pulse.Average).ToList(), LineStyle.Dotted)
            ];
        OnPropertyChanged(nameof(ChartSeries));
        OnPropertyChanged(nameof(ChartLabels));
    }

    private string Period(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc.ToUniversalTime(), _time.LocalTimeZone);
        return local.ToString(_shownInterval == StatisticsQuery.Month ? "Y" : "d", _l.Culture);
    }

    private string Cell(ValueStatsDto v) => string.Create(_l.Culture, $"{v.Average:0.#} ({v.Min}–{v.Max})");
}
