using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Maui.Tests;

[Category("Unit")]
public class ChartGeometryTests
{
    [Test]
    public async Task Range_HasPaddingAndNeverNegative()
    {
        var (min, max) = ChartGeometry.Range([[62.0, 130.0], [75.0]]);

        await Assert.That(min).IsEqualTo(50);
        await Assert.That(max).IsEqualTo(140);
        await Assert.That(ChartGeometry.Range([[3.0]]).Min).IsEqualTo(0);
    }

    [Test]
    public async Task Range_Empty_IsZeroToOne()
        => await Assert.That(ChartGeometry.Range([])).IsEqualTo((0.0, 1.0));

    [Test]
    public async Task SinglePoint_IsCentered()
        => await Assert.That(ChartGeometry.X(0, 1, 10, 100)).IsEqualTo(60);

    [Test]
    public async Task Y_ScalesValuesIntoPlotHeight()
    {
        await Assert.That(ChartGeometry.Y(0, 0, 100, 10, 200)).IsEqualTo(210);
        await Assert.That(ChartGeometry.Y(100, 0, 100, 10, 200)).IsEqualTo(10);
        await Assert.That(ChartGeometry.Y(50, 0, 100, 10, 200)).IsEqualTo(110);
    }
}

[Category("Unit")]
public class DateRangeTests
{
    [Test]
    public async Task LocalDays_BecomeInclusiveUtcBounds()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        var (from, to) = DateRange.ToUtcBounds(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), berlin);

        await Assert.That(from).IsEqualTo(new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc));
        await Assert.That(to).IsEqualTo(new DateTime(2026, 10, 31, 22, 59, 59, 999, DateTimeKind.Utc));
    }

    [Test]
    public async Task Null_StaysOpen()
    {
        var (from, to) = DateRange.ToUtcBounds(null, null, TimeZoneInfo.Utc);

        await Assert.That(from).IsNull();
        await Assert.That(to).IsNull();
    }
}
