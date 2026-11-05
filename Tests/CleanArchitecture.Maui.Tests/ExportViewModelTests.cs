using System.Net;
using CleanArchitecture.Presentation.Maui.Core.Services;
using CleanArchitecture.Presentation.Maui.Core.ViewModels;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class ExportViewModelTests
{
    private readonly FakeSecureStore _store = new();
    private readonly FakeAuthApi _auth = new();
    private readonly FakeBloodPressureApi _api = new();
    private readonly FakeNavigator _nav = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeShare _share = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = Clock.Fixed();
    private SessionService _session = null!;

    private async Task<ExportViewModel> CreateAsync(bool signedIn = true)
    {
        _session = new SessionService(_store, _auth, _time);
        if (signedIn)
        {
            await _session.LoginAsync("a@example.test", "pw");
        }

        return new ExportViewModel(_api, _session, _nav, _dialogs, _share, new ResxLocalizer(Culture.De), _time);
    }

    [Test]
    public async Task Csv_WithoutRange_SharesFileNamedAlleAlle()
    {
        var vm = await CreateAsync();

        await vm.ExportCommand.ExecuteAsync(null);

        await Assert.That(_api.ExportCalls[0]).IsEqualTo(("csv", (DateTime?)null, (DateTime?)null));
        await Assert.That(_share.Shared).IsEquivalentTo(new[] { ("blutdruck-alle-alle.csv", "a;b") });
        await Assert.That(vm.StatusMessage).IsEqualTo("Die Datei blutdruck-alle-alle.csv wurde bereitgestellt.");
        await Assert.That(_dialogs.Announcements).Contains(vm.StatusMessage!);
    }

    [Test]
    public async Task Pdf_WithRange_UsesUtcBoundsAndDescriptiveName()
    {
        var vm = await CreateAsync();
        vm.SelectedFormat = 1;
        vm.UseRange = true;
        vm.FromDate = new DateTime(2026, 10, 1);
        vm.ToDate = new DateTime(2026, 10, 31);

        await vm.ExportCommand.ExecuteAsync(null);

        await Assert.That(_api.ExportCalls[0].Format).IsEqualTo("pdf");
        await Assert.That(_api.ExportCalls[0].From).IsEqualTo(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        await Assert.That(_api.ExportCalls[0].To).IsEqualTo(new DateTime(2026, 10, 31, 23, 59, 59, 999, DateTimeKind.Utc));
        await Assert.That(_share.Shared[0].Name).IsEqualTo("blutdruck-2026-10-01-2026-10-31.pdf");
    }

    [Test]
    public async Task FromAfterTo_ShowsMessage_WithoutRequest()
    {
        var vm = await CreateAsync();
        vm.UseRange = true;
        vm.FromDate = new DateTime(2026, 11, 4);
        vm.ToDate = new DateTime(2026, 11, 1);

        await vm.ExportCommand.ExecuteAsync(null);

        await Assert.That(vm.Message).IsEqualTo("„Von“ darf nicht nach „Bis“ liegen.");
        await Assert.That(_api.ExportCalls).IsEmpty();
    }

    [Test]
    public async Task TooManyMeasurements_ShowsLocalizedHint()
    {
        var vm = await CreateAsync();
        _api.ExportFailWith = ApiExceptions.Create(HttpStatusCode.BadRequest, "{\"title\":\"x\"}");

        await vm.ExportCommand.ExecuteAsync(null);

        await Assert.That(vm.Message).Contains("zu viele");
        await Assert.That(_share.Shared).IsEmpty();
    }

    [Test]
    public async Task DuringExport_IsBusy_AndSecondCallIsIgnored()
    {
        var vm = await CreateAsync();
        var gate = new TaskCompletionSource<Stream>();
        _api.ExportGate = gate.Task;

        var first = vm.ExportCommand.ExecuteAsync(null);
        var busy = vm.IsBusy;
        await vm.ExportCommand.ExecuteAsync(null);
        gate.SetResult(new MemoryStream());
        await first;

        await Assert.That(busy).IsTrue();
        await Assert.That(_api.ExportCalls.Count).IsEqualTo(1);
        await Assert.That(vm.IsBusy).IsFalse();
    }

    [Test]
    public async Task Unauthorized_SignsOut_AndNavigatesToLogin()
    {
        var vm = await CreateAsync();
        _api.ExportFailWith = ApiExceptions.Create(HttpStatusCode.Unauthorized);

        await vm.ExportCommand.ExecuteAsync(null);

        await Assert.That(_nav.Visited).IsEquivalentTo(new[] { "login" });
        await Assert.That(_session.IsAuthenticated).IsFalse();
    }

    [Test]
    public async Task ShareFails_ShowsGenericMessage_WithoutDetails()
    {
        var vm = await CreateAsync();
        _share.FailWith = new IOException("C:\\secret\\path");

        await vm.ExportCommand.ExecuteAsync(null);

        await Assert.That(vm.Message).IsEqualTo("Etwas ist schiefgegangen. Bitte versuche es später erneut.");
    }
}
