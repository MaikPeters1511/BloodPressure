using CleanArchitecture.Application.BloodPressures;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Tests;

[Category("Unit")]
public class BloodPressureTests
{
    [Test]
    [Arguments(-1)]
    [Arguments(501)]
    public async Task Systolisch_OutOfRange_Throws(int value)
    {
        var entity = new BloodPressure();
        await Assert.That(() => entity.Systolisch = value).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(-1)]
    [Arguments(101)]
    public async Task Oxygen_OutOfRange_Throws(int value)
    {
        var entity = new BloodPressure();
        await Assert.That(() => entity.Oxygen = value).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Create_WithValidValues_ReturnsDtoWithId()
    {
        var repo = new FakeBloodPressureRepository();

        var dto = await BloodPressureHandler.Handle(
            new CreateBloodPressure("u1", 120, 80, DateTime.UtcNow, 65, 98), repo, TimeProvider.System, CancellationToken.None);

        await Assert.That(dto.Id).IsEqualTo(1);
        await Assert.That(dto.Systolisch).IsEqualTo(120);
    }

    [Test]
    public async Task GetById_Unknown_ReturnsNull()
    {
        var result = await BloodPressureHandler.Handle(
            new GetBloodPressureById("u1", 42), new FakeBloodPressureRepository(), CancellationToken.None);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Update_ExistingEntry_ChangesValues()
    {
        var repo = new FakeBloodPressureRepository();
        var created = await BloodPressureHandler.Handle(
            new CreateBloodPressure("u1", 120, 80, DateTime.UtcNow, 65, 98), repo, TimeProvider.System, CancellationToken.None);

        var ok = await BloodPressureHandler.Handle(
            new UpdateBloodPressure("u1", created.Id, 130, 85, DateTime.UtcNow, 70, 97), repo, CancellationToken.None);
        var reloaded = await BloodPressureHandler.Handle(new GetBloodPressureById("u1", created.Id), repo, CancellationToken.None);

        await Assert.That(ok).IsTrue();
        await Assert.That(reloaded!.Systolisch).IsEqualTo(130);
    }

    [Test]
    public async Task Delete_Unknown_ReturnsFalse()
    {
        var ok = await BloodPressureHandler.Handle(
            new DeleteBloodPressure("u1", 7), new FakeBloodPressureRepository(), CancellationToken.None);

        await Assert.That(ok).IsFalse();
    }
}
