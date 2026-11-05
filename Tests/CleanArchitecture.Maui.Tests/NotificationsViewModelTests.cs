using System.Net;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class NotificationsViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeNotificationApi _api = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = Clock.Fixed();
    private SessionService _session = null!;
    private NotificationCounter _counter = null!;

    private static NotificationDto Note(string type, bool read, int minute, string? category = null)
        => new(Guid.NewGuid(), type, category, "Text " + minute, new DateTime(2026, 11, 4, 8, minute, 0, DateTimeKind.Utc), read);

    private async Task<NotificationsViewModel> CreateAsync(bool signedIn = true, params NotificationDto[] items)
    {
        _api.Items.AddRange(items);
        _session = new SessionService(_store, _auth, _time);
        if (signedIn)
        {
            await _session.LoginAsync("a@example.test", "pw");
        }

        _counter = new NotificationCounter(_api);
        return new NotificationsViewModel(_api, _counter, _session, _nav, _dialogs, new ResxLocalizer(Culture.De), _time);
    }

    [Test]
    public async Task NotSignedIn_NavigatesToLogin()
    {
        var vm = await CreateAsync(signedIn: false);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
    }

    [Test]
    public async Task Initialize_ShowsNewestFirst_AndSetsUnreadCounter()
    {
        var vm = await CreateAsync(true, Note("Info", true, 1), Note("Info", false, 9), Note("Info", false, 5));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items[0].Message).IsEqualTo("Text 9");
        await Assert.That(_counter.Unread).IsEqualTo(2);
    }

    [Test]
    public async Task Empty_ShowsEmptyState()
    {
        var vm = await CreateAsync();

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.IsEmpty).IsTrue();
    }

    [Test]
    public async Task CriticalReading_IsWarning_WithCategoryText_AndScreenreaderSummary()
    {
        var vm = await CreateAsync(true, Note("CriticalReading", false, 5, "Hypertension"));

        await vm.InitializeCommand.ExecuteAsync(null);

        var item = vm.Items[0];
        await Assert.That(item.IsCritical).IsTrue();
        await Assert.That(item.Title).IsEqualTo("Warnung");
        await Assert.That(item.CategoryText).IsEqualTo("Bluthochdruck");
        await Assert.That(item.CategoryLevel).IsEqualTo(CategoryLevel.High);
        await Assert.That(item.Summary).Contains("Warnung");
        await Assert.That(item.Summary).Contains("Neu");
        await Assert.That(item.Summary).Contains("stark erhöhten Blutdrucks");
    }

    [Test]
    public async Task CriticalReading_UsesLocalizedText_NotTheServerText()
    {
        var vm = await CreateAsync(true, Note("CriticalReading", false, 5, "Hypertension"));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items[0].Message).Contains("stark erhöhten Blutdrucks");
        await Assert.That(vm.Items[0].Message).Contains("Bluthochdruck");
        await Assert.That(vm.Items[0].Message).DoesNotContain("Text 5");
    }

    [Test]
    public async Task CriticalReading_InEnglish_IsEnglish()
    {
        _api.Items.Add(Note("CriticalReading", false, 5, "Hypertension"));
        _session = new SessionService(_store, _auth, _time);
        await _session.LoginAsync("a@example.test", "pw");
        _counter = new NotificationCounter(_api);
        var vm = new NotificationsViewModel(_api, _counter, _session, _nav, _dialogs, new ResxLocalizer(Culture.En), _time);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items[0].Message).Contains("severely elevated blood pressure");
        await Assert.That(vm.Items[0].Message).Contains("Hypertension");
    }

    [Test]
    public async Task Reminder_UsesLocalizedText()
    {
        var vm = await CreateAsync(true, Note("MeasurementReminder", false, 5));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items[0].Message).IsEqualTo("Zeit für deine Blutdruckmessung.");
    }

    [Test]
    public async Task UnknownType_FallsBackToTheServerText()
    {
        var vm = await CreateAsync(true, Note("Something", false, 5));

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Items[0].Message).IsEqualTo("Text 5");
    }

    [Test]
    public async Task MarkRead_CallsApi_LowersCounter_AndAnnounces()
    {
        var vm = await CreateAsync(true, Note("CriticalReading", false, 5), Note("MeasurementReminder", false, 6));
        await vm.InitializeCommand.ExecuteAsync(null);

        await vm.MarkReadCommand.ExecuteAsync(vm.Items[0]);

        await Assert.That(_api.MarkedRead.Count).IsEqualTo(1);
        await Assert.That(_counter.Unread).IsEqualTo(1);
        await Assert.That(vm.Items.Count(i => !i.IsRead)).IsEqualTo(1);
        await Assert.That(_dialogs.Announcements).Contains("Der Hinweis wurde als gelesen markiert.");
    }

    [Test]
    public async Task Unauthorized_SignsOut()
    {
        var vm = await CreateAsync();
        _api.GetFailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task LoadFails_ShowsGenericMessage()
    {
        var vm = await CreateAsync();
        _api.GetFailWith = new HttpRequestException("secret host");

        await vm.InitializeCommand.ExecuteAsync(null);

        await Assert.That(vm.Message).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
    }

    [Test]
    public async Task MarkReadFails_ShowsGenericMessage()
    {
        var vm = await CreateAsync(true, Note("CriticalReading", false, 5));
        await vm.InitializeCommand.ExecuteAsync(null);
        _api.MarkFailWith = new HttpRequestException("x");

        await vm.MarkReadCommand.ExecuteAsync(vm.Items[0]);

        await Assert.That(vm.Message).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
        await Assert.That(_counter.Unread).IsEqualTo(1);
    }
}

[Category("Unit")]
public class NotificationCounterTests
{
    [Test]
    public async Task Refresh_CountsUnread_AndRaisesChanged()
    {
        var api = new FakeNotificationApi();
        api.Items.Add(new NotificationDto(Guid.NewGuid(), "CriticalReading", null, "x", DateTime.UtcNow, false));
        api.Items.Add(new NotificationDto(Guid.NewGuid(), "MeasurementReminder", null, "y", DateTime.UtcNow, true));
        var counter = new NotificationCounter(api);
        var raised = 0;
        counter.Changed += () => raised++;

        await counter.RefreshAsync();

        await Assert.That(counter.Unread).IsEqualTo(1);
        await Assert.That(raised).IsEqualTo(1);
    }

    [Test]
    public async Task Refresh_Failure_KeepsCount_WithoutThrowing()
    {
        var api = new FakeNotificationApi { GetFailWith = new HttpRequestException("x") };
        var counter = new NotificationCounter(api);
        counter.Set(3);

        await counter.RefreshAsync();

        await Assert.That(counter.Unread).IsEqualTo(3);
    }

    [Test]
    public async Task Reset_SetsZero()
    {
        var counter = new NotificationCounter(new FakeNotificationApi());
        counter.Set(4);

        counter.Set(0);

        await Assert.That(counter.Unread).IsEqualTo(0);
    }
}
