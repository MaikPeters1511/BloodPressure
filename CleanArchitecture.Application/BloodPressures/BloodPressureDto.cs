using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.BloodPressures;

public sealed record BloodPressureDto(int Id, int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen)
{
    public static BloodPressureDto From(BloodPressure entity)
        => new(entity.Id, entity.Systolisch, entity.Diastolisch, entity.DateTime, entity.Pulse, entity.Oxygen);
}
