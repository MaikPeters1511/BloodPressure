using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Application.BloodPressures;

internal static class BloodPressureMapping
{
    public static BloodPressureDto ToDto(BloodPressure entity)
        => new(entity.Id, entity.Systolisch, entity.Diastolisch, entity.DateTime, entity.Pulse, entity.Oxygen,
            BloodPressureClassifier.Classify(entity.Systolisch, entity.Diastolisch).Category.ToString());
}
