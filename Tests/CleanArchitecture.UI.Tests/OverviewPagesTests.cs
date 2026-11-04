using System.Net;
using Bunit;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI;
using CleanArchitecture.Presentation.UI.Layout;
using CleanArchitecture.Presentation.UI.Pages;
using CleanArchitecture.Presentation.UI.Services;
using Microsoft.AspNetCore.Components;

namespace CleanArchitecture.UI.Tests;

[Category("Unit")]
public class StatisticsPageTests
{
    private static StatisticsBucketDto Bucket(int day, int sys) => new(
        new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc), 2,
        new ValueStatsDto(sys, sys - 5, sys + 5), new ValueStatsDto(80, 75, 85), new ValueStatsDto(65, 60, 70));

    private static async Task<(UiHost Host, IRenderedComponent<Statistics> Cut)> OpenAsync(params StatisticsBucketDto[] buckets)
    {
        var host = new UiHost();
        host.Api.Buckets.AddRange(buckets);
        await host.SignInAsync();
        var cut = host.Context.Render<Statistics>();
        cut.WaitForState(() => cut.FindAll("[aria-busy=true]").Count == 0);
        return (host, cut);
    }

    [Test]
    public async Task NotSignedIn_RedirectsToLogin()
    {
        using var host = new UiHost();
        host.Context.Render<Statistics>();
        await Assert.That(host.CurrentPath).IsEqualTo("/login");
        await Assert.That(host.Api.StatisticsCalls).IsEmpty();
    }

    [Test]
    public async Task WithData_ShowsTableAndAccessibleChart()
    {
        var (host, cut) = await OpenAsync(Bucket(1, 120), Bucket(2, 130));
        using var _ = host;

        await Assert.That(cut.FindAll("#stats-table tbody tr").Count).IsEqualTo(2);
        await Assert.That(cut.Find("#stats-table tbody tr").TextContent).Contains("120");
        var svg = cut.Find("svg[role=img]");
        await Assert.That(svg.GetAttribute("aria-label")).IsNotEmpty();
        await Assert.That(cut.FindAll("polyline.series").Count).IsEqualTo(3);
        // Reihen sind nicht nur über die Farbe unterscheidbar: Legende mit Namen und unterschiedliche Linienform.
        await Assert.That(cut.FindAll("figcaption .legend-item").Count).IsEqualTo(3);
        await Assert.That(cut.FindAll("polyline.series").Select(p => p.GetAttribute("stroke-dasharray")).Distinct().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task DefaultRange_IsLastThirtyDaysWithDayInterval()
    {
        var (host, _) = await OpenAsync();
        using var h = host;

        var call = host.Api.StatisticsCalls[0];
        await Assert.That(call.Interval).IsEqualTo("day");
        await Assert.That(call.From).IsEqualTo(new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(call.To).IsEqualTo(new DateTime(2026, 11, 4, 23, 59, 59, 999, DateTimeKind.Utc));
    }

    [Test]
    public async Task ChoosingWeek_CallsApiWithWeekInterval()
    {
        var (host, cut) = await OpenAsync(Bucket(1, 120));
        using var _ = host;

        cut.Find("#stats-interval").Change("week");
        cut.Find("#stats-form").Submit();

        cut.WaitForState(() => host.Api.StatisticsCalls.Count == 2);
        await Assert.That(host.Api.StatisticsCalls[1].Interval).IsEqualTo("week");
    }

    [Test]
    public async Task NoData_ShowsEmptyState()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        await Assert.That(cut.Find("#stats-empty").TextContent).Contains("keine Messungen");
        await Assert.That(cut.FindAll("table").Count).IsEqualTo(0);
    }

    [Test]
    public async Task FromAfterTo_ShowsMessage_AndSendsNoRequest()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        cut.Find("#stats-from").Change("2026-11-04");
        cut.Find("#stats-to").Change("2026-11-01");
        cut.Find("#stats-form").Submit();

        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("nicht nach");
        await Assert.That(host.Api.StatisticsCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Unauthorized_SignsOutAndRedirects()
    {
        var host = new UiHost();
        using var _ = host;
        host.Api.StatisticsFailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);
        await host.SignInAsync();

        host.Context.Render<Statistics>();

        await Assert.That(host.CurrentPath).IsEqualTo("/login");
        await Assert.That(host.Session.IsAuthenticated).IsFalse();
    }
}

[Category("Unit")]
public class ExportPageTests
{
    [Test]
    public async Task NotSignedIn_RedirectsToLogin()
    {
        using var host = new UiHost();
        host.Context.Render<Export>();
        await Assert.That(host.CurrentPath).IsEqualTo("/login");
    }

    [Test]
    public async Task Csv_DownloadsFileWithDescriptiveName()
    {
        using var host = new UiHost();
        await host.SignInAsync();
        var cut = host.Context.Render<Export>();

        cut.Find("#export-from").Change("2026-10-01");
        cut.Find("#export-to").Change("2026-10-31");
        cut.Find("#export-form").Submit();

        cut.WaitForState(() => host.Browser.Downloads.Count == 1);
        await Assert.That(host.Api.ExportCalls[0].Format).IsEqualTo("csv");
        await Assert.That(host.Api.ExportCalls[0].From).IsEqualTo(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(host.Browser.Downloads[0].Name).IsEqualTo("blutdruck-2026-10-01-2026-10-31.csv");
        await Assert.That(host.Browser.Downloads[0].Content).IsEqualTo("a;b");
    }

    [Test]
    public async Task Pdf_WithoutRange_UsesPdfAndAllInName()
    {
        using var host = new UiHost();
        await host.SignInAsync();
        var cut = host.Context.Render<Export>();

        cut.Find("#export-format").Change("pdf");
        cut.Find("#export-form").Submit();

        cut.WaitForState(() => host.Browser.Downloads.Count == 1);
        await Assert.That(host.Api.ExportCalls[0]).IsEqualTo(("pdf", (DateTime?)null, (DateTime?)null));
        await Assert.That(host.Browser.Downloads[0].Name).IsEqualTo("blutdruck-alle-alle.pdf");
    }

    [Test]
    public async Task TooManyMeasurements_ShowsLocalizedHint()
    {
        using var host = new UiHost();
        await host.SignInAsync();
        host.Api.ExportFailWith = ApiExceptions.Create(HttpStatusCode.BadRequest, "{\"title\":\"x\"}");
        var cut = host.Context.Render<Export>();

        cut.Find("#export-form").Submit();

        cut.WaitForState(() => cut.FindAll("[role=alert]").Count == 1);
        await Assert.That(cut.Find("[role=alert]").TextContent).Contains("zu viele");
        await Assert.That(host.Browser.Downloads).IsEmpty();
    }

    [Test]
    public async Task DuringDownload_ButtonIsDisabledAndStatusAnnounced()
    {
        using var host = new UiHost();
        await host.SignInAsync();
        var gate = new TaskCompletionSource<Stream>();
        host.Api.ExportGate = gate.Task;
        var cut = host.Context.Render<Export>();

        cut.Find("#export-form").Submit();

        await Assert.That(cut.Find("#export-button").HasAttribute("disabled")).IsTrue();
        await Assert.That(cut.Find("#export-form").GetAttribute("aria-busy")).IsEqualTo("true");
        await Assert.That(cut.Find("#export-busy").GetAttribute("role")).IsEqualTo("status");
        gate.SetResult(new MemoryStream());
        cut.WaitForState(() => !cut.Find("#export-button").HasAttribute("disabled"));
    }
}

[Category("Unit")]
public class RemindersPageTests
{
    private static async Task<(UiHost Host, IRenderedComponent<Reminders> Cut)> OpenAsync(ReminderSettingsDto? stored = null)
    {
        var host = new UiHost();
        if (stored is not null)
        {
            host.Reminders.Stored = stored;
        }

        await host.SignInAsync();
        var cut = host.Context.Render<Reminders>();
        cut.WaitForState(() => cut.FindAll("[aria-busy=true]").Count == 0);
        return (host, cut);
    }

    [Test]
    public async Task NotSignedIn_RedirectsToLogin()
    {
        using var host = new UiHost();
        host.Context.Render<Reminders>();
        await Assert.That(host.CurrentPath).IsEqualTo("/login");
    }

    [Test]
    public async Task NoStoredTimes_ProposesBrowserTimeZone()
    {
        var (host, cut) = await OpenAsync();
        using var _ = host;

        await Assert.That(cut.Find("#reminder-zone").GetAttribute("value")).IsEqualTo("Europe/Berlin");
    }

    [Test]
    public async Task Save_SendsZoneAndTimes_AndConfirms()
    {
        var (host, cut) = await OpenAsync(new ReminderSettingsDto("Europe/Berlin", ["08:00"]));
        using var _ = host;

        cut.Find("#add-time").Click();
        cut.Find("#reminder-time-1").Change("20:30");
        cut.Find("#reminders-form").Submit();

        cut.WaitForState(() => cut.FindAll("#reminders-saved").Count == 1);
        await Assert.That(host.Reminders.Puts[0].TimeZoneId).IsEqualTo("Europe/Berlin");
        await Assert.That(host.Reminders.Puts[0].Times).IsEquivalentTo(new[] { "08:00", "20:30" });
        await Assert.That(cut.Find("#reminders-saved").GetAttribute("role")).IsEqualTo("status");
    }

    [Test]
    public async Task FiveTimes_DisableAdd()
    {
        var (host, cut) = await OpenAsync(new ReminderSettingsDto("UTC", ["06:00", "08:00", "10:00", "12:00", "14:00"]));
        using var _ = host;

        await Assert.That(cut.Find("#add-time").HasAttribute("disabled")).IsTrue();
    }

    [Test]
    public async Task DuplicateTimes_ShowFieldMessage_AndSendNothing()
    {
        var (host, cut) = await OpenAsync(new ReminderSettingsDto("UTC", ["08:00", "08:00"]));
        using var _ = host;

        cut.Find("#reminders-form").Submit();

        await Assert.That(cut.Find("#reminders-errors").TextContent).Contains("nur einmal");
        await Assert.That(host.Reminders.Puts).IsEmpty();
    }

    [Test]
    public async Task ServerValidationError_IsShownInErrorRegion()
    {
        var (host, cut) = await OpenAsync(new ReminderSettingsDto("Mars/Base", ["08:00"]));
        using var _ = host;
        host.Reminders.PutFailWith = ApiExceptions.Validation("timeZoneId", "Unbekannte Zeitzone.");

        cut.Find("#reminders-form").Submit();

        cut.WaitForState(() => cut.Find("#reminders-errors").TextContent.Contains("Unbekannte Zeitzone"));
        await Assert.That(cut.Find("#reminders-errors [role=alert]").TextContent).Contains("Unbekannte Zeitzone.");
        host.Context.JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public async Task RemoveButton_MovesFocusToAddButton()
    {
        var (host, cut) = await OpenAsync(new ReminderSettingsDto("UTC", ["08:00", "20:00"]));
        using var _ = host;

        cut.FindAll(".remove-time")[0].Click();

        host.Context.JSInterop.VerifyFocusAsyncInvoke();
    }

    [Test]
    public async Task RemoveButton_DropsTime()
    {
        var (host, cut) = await OpenAsync(new ReminderSettingsDto("UTC", ["08:00", "20:00"]));
        using var _ = host;

        cut.FindAll(".remove-time")[0].Click();

        await Assert.That(cut.FindAll(".reminder-row").Count).IsEqualTo(1);
    }
}

[Category("Unit")]
public class NotificationsPageTests
{
    private static NotificationDto Note(string type, bool read, int minute, string? category = null)
        => new(Guid.NewGuid(), type, category, "Text " + minute, new DateTime(2026, 11, 4, 8, minute, 0, DateTimeKind.Utc), read);

    private static async Task<(UiHost Host, IRenderedComponent<Notifications> Cut)> OpenAsync(params NotificationDto[] items)
    {
        var host = new UiHost();
        host.Notifications.Items.AddRange(items);
        await host.SignInAsync();
        var cut = host.Context.Render<Notifications>();
        cut.WaitForState(() => cut.FindAll("[aria-busy=true]").Count == 0);
        return (host, cut);
    }

    [Test]
    public async Task NotSignedIn_RedirectsToLogin()
    {
        using var host = new UiHost();
        host.Context.Render<Notifications>();
        await Assert.That(host.CurrentPath).IsEqualTo("/login");
    }

    [Test]
    public async Task ShowsNewestFirst_AndEmptyState()
    {
        var (host, cut) = await OpenAsync(Note("MeasurementReminder", true, 1), Note("MeasurementReminder", true, 9));
        using var _ = host;
        await Assert.That(cut.FindAll("li.notification")[0].TextContent).Contains("Text 9");

        var (host2, cut2) = await OpenAsync();
        using var __ = host2;
        await Assert.That(cut2.Find("#notifications-empty").TextContent).IsNotEmpty();
    }

    [Test]
    public async Task UnreadCritical_IsMarkedAsWarningWithAlertRole()
    {
        var (host, cut) = await OpenAsync(Note("CriticalReading", false, 5, "Grade3"));
        using var _ = host;

        var item = cut.Find("li.notification");
        // Die Rolle sitzt im Inneren: ein <li> mit role=alert wäre kein Listenelement mehr (axe: list).
        await Assert.That(item.GetAttribute("role")).IsNull();
        await Assert.That(item.QuerySelector("[role=alert]")).IsNotNull();
        await Assert.That(item.TextContent).Contains("Warnung");
        await Assert.That(item.TextContent).Contains("Hypertonie Grad 3");
    }

    [Test]
    public async Task MarkRead_CallsApi_AndLowersCounter()
    {
        var (host, cut) = await OpenAsync(Note("CriticalReading", false, 5), Note("MeasurementReminder", false, 6));
        using var _ = host;
        var counter = host.Context.Services.GetService(typeof(NotificationCounter)) as NotificationCounter;
        await Assert.That(counter!.Unread).IsEqualTo(2);

        cut.Find("button.mark-read").Click();

        cut.WaitForState(() => counter.Unread == 1);
        await Assert.That(host.Notifications.MarkedRead.Count).IsEqualTo(1);
        await Assert.That(cut.FindAll("button.mark-read").Count).IsEqualTo(1);
    }

    [Test]
    public async Task MarkRead_AnnouncesStatus_AndKeepsKeyboardFocusInList()
    {
        var (host, cut) = await OpenAsync(Note("CriticalReading", false, 5));
        using var _ = host;

        cut.Find("button.mark-read").Click();

        host.Context.JSInterop.VerifyFocusAsyncInvoke();
        await Assert.That(cut.Find("#notifications-status").TextContent).Contains("gelesen markiert");
        await Assert.That(cut.Find("li.notification").GetAttribute("tabindex")).IsEqualTo("-1");
    }

    [Test]
    public async Task NavMenu_SingleUnread_UsesSingularLabel()
    {
        using var host = new UiHost();
        host.Notifications.Items.Add(Note("CriticalReading", false, 1));
        await host.SignInAsync();

        var cut = host.Context.Render<NavMenu>();

        cut.WaitForState(() => cut.FindAll(".unread-count").Count == 1);
        await Assert.That(cut.Find(".unread-count").GetAttribute("aria-label")).IsEqualTo("1 ungelesener Hinweis");
    }

    [Test]
    public async Task NavMenu_ShowsUnreadCountWithAccessibleLabel()
    {
        using var host = new UiHost();
        host.Notifications.Items.Add(Note("CriticalReading", false, 1));
        host.Notifications.Items.Add(Note("MeasurementReminder", false, 2));
        await host.SignInAsync();

        var cut = host.Context.Render<NavMenu>();

        cut.WaitForState(() => cut.FindAll(".unread-count").Count == 1);
        await Assert.That(cut.Find(".unread-count").GetAttribute("aria-label")).IsEqualTo("2 ungelesene Hinweise");
        await Assert.That(cut.Find(".unread-count").TextContent).IsEqualTo("2");
        await Assert.That(cut.FindAll("a[href=statistics], a[href=export], a[href=reminders], a[href=notifications]").Count).IsEqualTo(4);
    }
}

[Category("Unit")]
public class GeometryAndResourcesTests
{
    [Test]
    public async Task Points_AreInvariantAndScaled()
    {
        // min 0, max 100 auf Höhe 100, Breite 100: Mitte des Wertebereichs liegt in der Mitte.
        var points = ChartGeometry.Points([0, 50, 100], 0, 100, 0, 0, 100, 100);
        await Assert.That(points).IsEqualTo("0,100 50,50 100,0");
    }

    [Test]
    public async Task Range_HasPaddingAndNeverNegative()
    {
        var (min, max) = ChartGeometry.Range([[62.0, 130.0], [75.0]]);
        await Assert.That(min).IsEqualTo(50);
        await Assert.That(max).IsEqualTo(140);
        await Assert.That(ChartGeometry.Range([[3.0]]).Min).IsEqualTo(0);
    }

    [Test]
    public async Task SinglePoint_IsCentered()
        => await Assert.That(ChartGeometry.X(0, 1, 10, 100)).IsEqualTo(60);

    [Test]
    public async Task DateRange_ConvertsLocalDaysToInclusiveUtcBounds()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var result = DateRangeParser.TryParse("2026-10-01", "2026-10-31", berlin, out var from, out var to);

        await Assert.That(result).IsEqualTo(DateRangeParser.Result.Ok);
        await Assert.That(from).IsEqualTo(new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc));
        await Assert.That(to).IsEqualTo(new DateTime(2026, 10, 31, 22, 59, 59, 999, DateTimeKind.Utc));
    }

    [Test]
    [Arguments("2026-11-02", "2026-11-01", DateRangeParser.Result.FromAfterTo)]
    [Arguments("kein-datum", "", DateRangeParser.Result.Invalid)]
    [Arguments("", "", DateRangeParser.Result.Ok)]
    public async Task DateRange_Validates(string from, string to, DateRangeParser.Result expected)
        => await Assert.That(DateRangeParser.TryParse(from, to, TimeZoneInfo.Utc, out _, out _)).IsEqualTo(expected);

    [Test]
    public async Task GermanAndEnglishResources_HaveTheSameKeys()
    {
        var de = Keys("SharedResource.resx");
        var en = Keys("SharedResource.en.resx");

        await Assert.That(de.Except(en).ToList()).IsEmpty();
        await Assert.That(en.Except(de).ToList()).IsEmpty();
    }

    [Test]
    public async Task EnglishCulture_RendersEnglishTextOnNewPages()
    {
        using var host = new UiHost("en");
        await host.SignInAsync();

        var cut = host.Context.Render<Export>();

        await Assert.That(cut.Markup).Contains("Download");
        await Assert.That(cut.Markup).DoesNotContain("Herunterladen");
    }

    [Test]
    public async Task AllLocalizerKeysUsedInNewPages_ExistInResources()
    {
        var keys = Keys("SharedResource.resx");
        var root = FindRoot();
        var used = new[] { "Pages/Statistics.razor", "Pages/Export.razor", "Pages/Reminders.razor", "Pages/Notifications.razor", "Layout/NavMenu.razor", "Components/CategoryBadge.razor" }
            .SelectMany(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(Path.Combine(root, "CleanArchitecture.Presentation.UI", f)), "L\\[\"(\\w+)\"")
                .Select(m => m.Groups[1].Value))
            .Distinct();

        await Assert.That(used.Except(keys).ToList()).IsEmpty();
    }

    private static HashSet<string> Keys(string file)
        => System.Xml.Linq.XDocument.Load(Path.Combine(FindRoot(), "CleanArchitecture.Presentation.UI", file))
            .Descendants("data").Select(d => d.Attribute("name")!.Value).ToHashSet();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }
}

[Category("Unit")]
public class NavMenuMobileTests
{
    [Test]
    public async Task Menu_StartsCollapsed_AndToggleKeepsAriaExpandedInSync()
    {
        using var host = new UiHost();
        await host.SignInAsync();
        var cut = host.Context.Render<NavMenu>();

        await Assert.That(cut.Find(".navbar-toggler").GetAttribute("aria-expanded")).IsEqualTo("false");
        await Assert.That(cut.Find("#main-nav").ClassList).Contains("collapse");

        cut.Find(".navbar-toggler").Click();

        await Assert.That(cut.Find(".navbar-toggler").GetAttribute("aria-expanded")).IsEqualTo("true");
        await Assert.That(cut.Find("#main-nav").ClassList).DoesNotContain("collapse");
        await Assert.That(cut.Find(".navbar-toggler").GetAttribute("aria-controls")).IsEqualTo("main-nav");
    }

    [Test]
    public async Task ClickOnEntry_CollapsesMenuAgain()
    {
        using var host = new UiHost();
        await host.SignInAsync();
        var cut = host.Context.Render<NavMenu>();
        cut.Find(".navbar-toggler").Click();

        cut.Find("a[href=statistics]").Click();

        await Assert.That(cut.Find(".navbar-toggler").GetAttribute("aria-expanded")).IsEqualTo("false");
    }
}
