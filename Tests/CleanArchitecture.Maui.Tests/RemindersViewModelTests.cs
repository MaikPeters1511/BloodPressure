using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class RemindersViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeReminderApi _api = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = Clock.Fixed();
    private SessionService _session = null!;

    private async Task<RemindersViewModel> CreateAsync(ReminderSettingsDto? stored = null, bool signedIn = true, string culture = "de")
    {
        if (stored is not null)
        {
            _api.Stored = stored;
        }

        _time.SetLocalTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"));
        _session = new SessionService(_store, _auth, _time);
        if (signedIn)
        {
            await _session.LoginAsync("a@example.test", "pw");
        }

        return new RemindersViewModel(_api, _session, _nav, _dialogs, new ResxLocalizer(culture == "de" ? Culture.De : Culture.En), _time);
    }

    [Test]
    public async Task NotSignedIn_NavigatesToLogin()
    {
        var vm = await CreateAsync(signedIn: false);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
    }

    [Test]
    public async Task Initialize_LoadsStoredZoneAndTimes()
    {
        var vm = await CreateAsync(new ReminderSettingsDto("Europe/Vienna", ["08:00", "20:30"]));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.TimeZoneId).IsEqualTo("Europe/Vienna");
        await Assert.That(vm.Times.Select(t => t.Time)).IsEquivalentTo(new[] { new TimeSpan(8, 0, 0), new TimeSpan(20, 30, 0) });
    }

    [Test]
    public async Task NoStoredTimes_ProposesDeviceTimeZone()
    {
        var vm = await CreateAsync();

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.TimeZoneId).IsEqualTo("Europe/Berlin");
        await Assert.That(vm.Times).IsEmpty();
    }

    [Test]
    public async Task AddTime_StopsAtFive()
    {
        var vm = await CreateAsync();
        await vm.InitializeCommand.ExecuteAsync(null);

        for (var i = 0; i < 7; i++)
        {
            vm.AddTimeCommand.Execute(null);
        }

        await Assert.That(vm.Times.Count).IsEqualTo(5);
        await Assert.That(vm.CanAdd).IsFalse();
    }

    [Test]
    public async Task RemoveTime_DropsItem_AndAnnounces()
    {
        var vm = await CreateAsync(new ReminderSettingsDto("UTC", ["08:00", "20:00"]));
        await vm.InitializeCommand.ExecuteAsync(null);

        vm.RemoveTimeCommand.Execute(vm.Times[0]);

        await Assert.That(vm.Times.Count).IsEqualTo(1);
        await Assert.That(vm.CanAdd).IsTrue();
        await Assert.That(_dialogs.Announcements.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Save_SendsZoneAndFormattedTimes_AndConfirms()
    {
        var vm = await CreateAsync(new ReminderSettingsDto("Europe/Berlin", ["08:00"]));
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.AddTimeCommand.Execute(null);
        vm.Times[1].Time = new TimeSpan(20, 30, 0);

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(_api.Puts[0].TimeZoneId).IsEqualTo("Europe/Berlin");
        await Assert.That(_api.Puts[0].Times).IsEquivalentTo(new[] { "08:00", "20:30" });
        await Assert.That(vm.StatusMessage).IsEqualTo("Die Erinnerungen wurden gespeichert.");
        await Assert.That(_dialogs.Announcements).Contains("Die Erinnerungen wurden gespeichert.");
    }

    [Test]
    public async Task DuplicateTimes_ShowMessage_AndSendNothing()
    {
        var vm = await CreateAsync(new ReminderSettingsDto("UTC", ["08:00", "08:00"]));
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Jede Uhrzeit darf nur einmal vorkommen." });
        await Assert.That(_api.Puts).IsEmpty();
    }

    [Test]
    public async Task EmptyZone_ShowsMessage()
    {
        var vm = await CreateAsync();
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.TimeZoneId = "  ";

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Bitte gib eine Zeitzone an." });
        await Assert.That(_api.Puts).IsEmpty();
    }

    [Test]
    public async Task ServerValidation_IsShown_AndAnnounced()
    {
        var vm = await CreateAsync(new ReminderSettingsDto("Mars/Base", ["08:00"]));
        await vm.InitializeCommand.ExecuteAsync(null);
        _api.PutFailWith = ApiExceptions.Validation("timeZoneId", "Unbekannte Zeitzone.");

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Unbekannte Zeitzone." });
        await Assert.That(_dialogs.Announcements).Contains("Unbekannte Zeitzone.");
        await Assert.That(vm.StatusMessage).IsNull();
    }

    [Test]
    public async Task Unauthorized_SignsOut()
    {
        var vm = await CreateAsync(new ReminderSettingsDto("UTC", ["08:00"]));
        await vm.InitializeCommand.ExecuteAsync(null);
        _api.PutFailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task LoadFails_ShowsGenericMessage()
    {
        var vm = await CreateAsync();
        _api.GetFailWith = new HttpRequestException("secret host");

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Etwas ist schiefgegangen. Bitte versuche es später erneut." });
    }

    [Test]
    public async Task English_UsesEnglishTexts()
    {
        var vm = await CreateAsync(new ReminderSettingsDto("UTC", ["08:00", "08:00"]), culture: "en");
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.SaveCommand.ExecuteAsync(null);

        await Assert.That(vm.Errors).IsEquivalentTo(new[] { "Each time may only appear once." });
    }
}
