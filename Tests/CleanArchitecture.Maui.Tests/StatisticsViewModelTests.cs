using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class StatisticsViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeBloodPressureApi _api = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = Clock.Fixed();
    private SessionService _session = null!;

    private static StatisticsBucketDto Bucket(int day, int sys) => new(
        new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc), 2,
        new ValueStatsDto(sys, sys - 5, sys + 5), new ValueStatsDto(80, 75, 85), new ValueStatsDto(65, 60, 70));

    private async Task<StatisticsViewModel> CreateAsync(bool signedIn = true, string culture = "de", params StatisticsBucketDto[] buckets)
    {
        _api.Buckets.AddRange(buckets);
        _session = new SessionService(_store, _auth, _time);
        if (signedIn)
        {
            await _session.LoginAsync("a@example.test", "pw");
        }

        return new StatisticsViewModel(_api, _session, _nav, _dialogs, new ResxLocalizer(culture == "de" ? Culture.De : Culture.En), _time);
    }

    [Test]
    public async Task NotSignedIn_NavigatesToLogin_WithoutApiCall()
    {
        var vm = await CreateAsync(signedIn: false);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_api.StatisticsCalls).IsEmpty();
    }

    [Test]
    public async Task Initialize_UsesLastThirtyDays_WithDayInterval()
    {
        var vm = await CreateAsync();

        await vm.InitializeCommand.ExecuteAsync(null);

        var call = _api.StatisticsCalls[0];
        await Assert.That(call.Interval).IsEqualTo("day");
        await Assert.That(call.From).IsEqualTo(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(call.To).IsEqualTo(new DateTime(2026, 11, 4, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Test]
    public async Task WithData_BuildsRowsAndThreeDistinguishableSeries()
    {
        var vm = await CreateAsync(true, "de", Bucket(1, 120), Bucket(2, 130));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Rows.Count).IsEqualTo(2);
        await Assert.That(vm.Rows[0].Systolic).IsEqualTo("120 (115–125)");
        await Assert.That(vm.ChartSeries.Count).IsEqualTo(3);
        await Assert.That(vm.ChartSeries.Select(s => s.Style).Distinct().Count()).IsEqualTo(3);
        await Assert.That(vm.ChartSeries[0].Values).IsEquivalentTo(new[] { 120.0, 130.0 });
        await Assert.That(vm.HasData).IsTrue();
        await Assert.That(vm.ChartDescription).Contains("Tabelle");
    }

    [Test]
    public async Task Row_HasCompleteScreenreaderSummary()
    {
        var vm = await CreateAsync(true, "de", Bucket(1, 120));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Rows[0].Summary).Contains("2 Messungen");
        await Assert.That(vm.Rows[0].Summary).Contains("Systolisch 120");
    }

    [Test]
    public async Task ChoosingWeek_CallsApiWithWeekInterval()
    {
        var vm = await CreateAsync(true, "de", Bucket(1, 120));
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.SelectedInterval = 1;

        await vm.ApplyCommand.ExecuteAsync(null);

        await Assert.That(_api.StatisticsCalls.Last().Interval).IsEqualTo("week");
    }

    [Test]
    public async Task NoData_ShowsEmptyState()
    {
        var vm = await CreateAsync();

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.IsEmpty).IsTrue();
        await Assert.That(vm.HasData).IsFalse();
    }

    [Test]
    public async Task FromAfterTo_ShowsMessage_AndSendsNoRequest()
    {
        var vm = await CreateAsync();
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.FromDate = new DateTime(2026, 11, 4);
        vm.ToDate = new DateTime(2026, 11, 1);

        await vm.ApplyCommand.ExecuteAsync(null);

        await Assert.That(vm.Message).IsEqualTo("„Von“ darf nicht nach „Bis“ liegen.");
        await Assert.That(_api.StatisticsCalls.Count).IsEqualTo(1);
        await Assert.That(_dialogs.Announcements).Contains("„Von“ darf nicht nach „Bis“ liegen.");
    }

    [Test]
    public async Task Unauthorized_SignsOut_AndNavigatesToLogin()
    {
        var vm = await CreateAsync();
        _api.StatisticsFailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task Error_ShowsGenericMessage_WithoutDetails()
    {
        var vm = await CreateAsync();
        _api.StatisticsFailWith = new HttpRequestException("db password=secret");

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Message).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
        await Assert.That(vm.HasData).IsFalse();
    }

    [Test]
    public async Task EnglishCulture_UsesEnglishTexts()
    {
        var vm = await CreateAsync(true, "en");

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.IntervalOptions).IsEquivalentTo(new[] { "Day", "Week", "Month" });
    }
}
