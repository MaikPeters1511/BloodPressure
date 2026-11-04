using CleanArchitecture.Application.Reminders;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using CleanArchitecture.Domain.IRepository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CleanArchitecture.Tests;

[Category("Unit")]
public class ReminderProcessorTests
{
    private const string Berlin = "Europe/Berlin"; // Im November UTC+1.

    private readonly FakeTimeProvider _time = new();
    private readonly FakeReminderRepository _reminders = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeBloodPressureRepository _measurements = new();

    private ReminderProcessor Processor() =>
        new(_reminders, _measurements, _notifications, _time, NullLogger<ReminderProcessor>.Instance);

    private void SetBerlinTime(int hour, int minute) =>
        _time.SetUtcNow(new DateTimeOffset(2026, 11, 4, hour - 1, minute, 0, TimeSpan.Zero));

    private Reminder AddReminder(int hour, int minute = 0, string user = "u1") =>
        _reminders.Add(new Reminder { UserId = user, TimeOfDay = new TimeOnly(hour, minute), TimeZoneId = Berlin });

    private Task MeasureAsync(DateTime utc) => _measurements.AddAsync(new BloodPressure
    {
        UserId = "u1", Systolisch = 120, Diastolisch = 80, DateTime = utc, Pulse = 60, Oxygen = 98
    });

    [Test]
    public async Task DueReminder_WithoutMeasurementToday_CreatesNotification()
    {
        AddReminder(8);
        SetBerlinTime(8, 0);

        await Processor().ProcessDueAsync(CancellationToken.None);

        var notification = _notifications.Items.Single();
        await Assert.That(notification.Type).IsEqualTo(NotificationType.MeasurementReminder);
        await Assert.That(notification.UserId).IsEqualTo("u1");
    }

    [Test]
    public async Task ReminderNotYetDue_CreatesNothing()
    {
        AddReminder(8);
        SetBerlinTime(7, 59);

        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items).IsEmpty();
    }

    [Test]
    public async Task MeasurementShortlyBefore_SuppressesNotification()
    {
        AddReminder(8);
        await MeasureAsync(new DateTime(2026, 11, 4, 6, 55, 0, DateTimeKind.Utc)); // 07:55 Ortszeit
        SetBerlinTime(8, 0);

        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items).IsEmpty();
    }

    [Test]
    public async Task MeasurementLongBefore_DoesNotSuppressNotification()
    {
        AddReminder(20);
        await MeasureAsync(new DateTime(2026, 11, 4, 7, 10, 0, DateTimeKind.Utc)); // 08:10 Ortszeit, 12 h vorher
        SetBerlinTime(20, 0);

        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SameReminder_FiresOnlyOncePerDay()
    {
        AddReminder(8);
        SetBerlinTime(8, 0);
        await Processor().ProcessDueAsync(CancellationToken.None);

        SetBerlinTime(8, 5);
        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items.Count).IsEqualTo(1);
    }

    [Test]
    public async Task NextDay_FiresAgain()
    {
        AddReminder(8);
        SetBerlinTime(8, 0);
        await Processor().ProcessDueAsync(CancellationToken.None);

        _time.SetUtcNow(new DateTimeOffset(2026, 11, 5, 7, 0, 0, TimeSpan.Zero));
        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task MissedBeyondGrace_IsClosedWithoutNotification()
    {
        var reminder = AddReminder(8);
        SetBerlinTime(9, 0); // Dienst war eine Stunde aus

        await Processor().ProcessDueAsync(CancellationToken.None);
        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items).IsEmpty();
        await Assert.That(reminder.LastFiredOn).IsEqualTo(new DateOnly(2026, 11, 4));
    }

    [Test]
    public async Task EachUserHasTheirOwnTimeZone()
    {
        AddReminder(8, user: "berlin");
        _reminders.Add(new Reminder { UserId = "tokyo", TimeOfDay = new TimeOnly(8, 0), TimeZoneId = "Asia/Tokyo" });
        SetBerlinTime(8, 0); // 07:00 UTC = 16:00 in Tokyo, dort längst vorbei

        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items.Select(n => n.UserId).ToArray()).IsEquivalentTo(new[] { "berlin" });
    }

    [Test]
    public async Task UnknownTimeZone_IsSkippedWithoutFailing()
    {
        _reminders.Add(new Reminder { UserId = "u1", TimeOfDay = new TimeOnly(8, 0), TimeZoneId = "Mars/Olympus" });
        AddReminder(8, user: "u2");
        SetBerlinTime(8, 0);

        await Processor().ProcessDueAsync(CancellationToken.None);

        await Assert.That(_notifications.Items.Single().UserId).IsEqualTo("u2");
    }

    [Test]
    [Arguments(9, 0, true)]
    [Arguments(8, 0, true)]
    [Arguments(7, 59, false)]
    public async Task SavingSettings_DoesNotFireTimesThatAlreadyPassedToday(int hour, int minute, bool markedAsFired)
    {
        SetBerlinTime(hour, minute);

        await ReminderHandler.Handle(
            new SetReminders("u1", new ReminderSettingsDto(Berlin, ["08:00"])), _reminders, _time, CancellationToken.None);

        var stored = _reminders.Items.Single();
        await Assert.That(stored.LastFiredOn is not null).IsEqualTo(markedAsFired);
    }

    private sealed class FakeReminderRepository : IReminderRepository
    {
        public List<Reminder> Items { get; } = [];

        public Reminder Add(Reminder reminder)
        {
            Items.Add(reminder);
            return reminder;
        }

        public Task<IReadOnlyList<Reminder>> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Reminder>>(Items.Where(r => r.UserId == userId).ToList());

        public Task<IReadOnlyList<Reminder>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Reminder>>(Items.ToList());

        public Task ReplaceForUserAsync(string userId, IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(r => r.UserId == userId);
            Items.AddRange(reminders);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification> Items { get; } = [];

        public Task<IReadOnlyList<Notification>> GetForUserAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Notification>>(Items.Where(n => n.UserId == userId).ToList());

        public Task<Notification?> GetByIdAsync(Guid id, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(n => n.Id == id && n.UserId == userId));

        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Items.Add(notification);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
