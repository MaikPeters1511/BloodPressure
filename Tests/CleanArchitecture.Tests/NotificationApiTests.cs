using System.Net;
using CleanArchitecture.Contracts;
using Refit;

namespace CleanArchitecture.Tests;

/// <summary>Benachrichtigungen und Erinnerungen über die echte API gegen PostgreSQL.</summary>
[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class NotificationApiTests(PostgresFixture postgres)
{
    private static readonly DateTime Measured = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task CriticalReading_CreatesExactlyOneUnreadNotification_WithoutValues()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var http = await RealApiHarness.SignInAsync(factory, "a@example.test");
        var measurements = RestService.For<IBloodPressureApi>(http);
        var notifications = RestService.For<INotificationApi>(http);

        await measurements.CreateAsync(new BloodPressureRequest(185, 115, Measured, 70, 97));
        var list = await notifications.GetAllAsync();

        var notification = list.Single();
        await Assert.That(notification.Type).IsEqualTo("CriticalReading");
        await Assert.That(notification.Category).IsEqualTo("Grade3");
        await Assert.That(notification.IsRead).IsFalse();
        await Assert.That(notification.Message).Contains("keine Diagnose");
        await Assert.That(notification.Message).DoesNotContain("185");
        await Assert.That(notification.Message).DoesNotContain("115");
    }

    [Test]
    public async Task NormalReading_CreatesNoNotification()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var http = await RealApiHarness.SignInAsync(factory, "a@example.test");

        await RestService.For<IBloodPressureApi>(http).CreateAsync(new BloodPressureRequest(118, 76, Measured, 65, 98));

        await Assert.That(await RestService.For<INotificationApi>(http).GetAllAsync()).IsEmpty();
    }

    [Test]
    public async Task MarkAsRead_MakesNotificationRead_AndOnlyForTheOwner()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var userA = await RealApiHarness.SignInAsync(factory, "a@example.test");
        var userB = await RealApiHarness.SignInAsync(factory, "b@example.test");
        await RestService.For<IBloodPressureApi>(userA).CreateAsync(new BloodPressureRequest(185, 115, Measured, 70, 97));
        var apiA = RestService.For<INotificationApi>(userA);
        var apiB = RestService.For<INotificationApi>(userB);
        var id = (await apiA.GetAllAsync()).Single().Id;

        var foreign = await Assert.That(async () => await apiB.MarkAsReadAsync(id)).Throws<ApiException>();
        await apiA.MarkAsReadAsync(id);

        await Assert.That((int)foreign!.StatusCode).IsEqualTo(404);
        await Assert.That((await apiA.GetAllAsync()).Single().IsRead).IsTrue();
        await Assert.That(await apiB.GetAllAsync()).IsEmpty();
    }

    [Test]
    public async Task Notifications_ListsUnreadFirst()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var http = await RealApiHarness.SignInAsync(factory, "a@example.test");
        var measurements = RestService.For<IBloodPressureApi>(http);
        var api = RestService.For<INotificationApi>(http);
        await measurements.CreateAsync(new BloodPressureRequest(185, 115, Measured, 70, 97));
        var first = (await api.GetAllAsync()).Single().Id;
        await measurements.CreateAsync(new BloodPressureRequest(190, 120, Measured.AddHours(1), 70, 97));
        await api.MarkAsReadAsync(first);

        var list = await api.GetAllAsync();

        await Assert.That(list.Count).IsEqualTo(2);
        await Assert.That(list[0].IsRead).IsFalse();
        await Assert.That(list[1].Id).IsEqualTo(first);
    }

    [Test]
    public async Task Reminders_PutThenGet_ReturnsSortedTimes()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IReminderApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));

        await api.PutAsync(new ReminderSettingsDto("Europe/Berlin", ["20:00", "08:00"]));
        var settings = await api.GetAsync();

        await Assert.That(settings.TimeZoneId).IsEqualTo("Europe/Berlin");
        await Assert.That(settings.Times.ToArray()).IsEquivalentTo(new[] { "08:00", "20:00" });
        await Assert.That(settings.Times[0]).IsEqualTo("08:00");
    }

    [Test]
    public async Task Reminders_Put_ReplacesPreviousSettings()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IReminderApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));

        await api.PutAsync(new ReminderSettingsDto("Europe/Berlin", ["08:00", "20:00"]));
        await api.PutAsync(new ReminderSettingsDto("Europe/Berlin", ["09:30"]));

        await Assert.That((await api.GetAsync()).Times.Single()).IsEqualTo("09:30");
    }

    [Test]
    public async Task Reminders_AreSeparatedPerUser()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var userA = RestService.For<IReminderApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));
        var userB = RestService.For<IReminderApi>(await RealApiHarness.SignInAsync(factory, "b@example.test"));

        await userA.PutAsync(new ReminderSettingsDto("Europe/Berlin", ["08:00"]));

        await Assert.That((await userB.GetAsync()).Times).IsEmpty();
    }

    [Test]
    [Arguments("Europe/Berlin", new[] { "06:00", "07:00", "08:00", "09:00", "10:00", "11:00" })]
    [Arguments("Europe/Berlin", new[] { "08:00", "08:00" })]
    [Arguments("Europe/Berlin", new[] { "8:00" })]
    [Arguments("Europe/Berlin", new[] { "25:00" })]
    [Arguments("Mars/Olympus", new[] { "08:00" })]
    public async Task Reminders_InvalidInput_Returns400(string timeZone, string[] times)
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IReminderApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));

        var ex = await Assert.That(async () => await api.PutAsync(new ReminderSettingsDto(timeZone, times))).Throws<ApiException>();

        await Assert.That(ex!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Endpoints_WithoutToken_Return401()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var http = factory.CreateClient();

        await Assert.That((await http.GetAsync("/api/notifications")).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That((await http.GetAsync("/api/reminders")).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
