using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Tests;

[Category("Unit")]
public class ClassifierTests
{
    [Test]
    [Arguments(118, 76, BloodPressureCategory.Optimal)]
    [Arguments(119, 79, BloodPressureCategory.Optimal)]
    [Arguments(120, 79, BloodPressureCategory.Normal)]
    [Arguments(119, 80, BloodPressureCategory.Normal)]
    [Arguments(129, 84, BloodPressureCategory.Normal)]
    [Arguments(130, 80, BloodPressureCategory.HighNormal)]
    [Arguments(120, 85, BloodPressureCategory.HighNormal)]
    [Arguments(139, 89, BloodPressureCategory.HighNormal)]
    [Arguments(140, 85, BloodPressureCategory.Grade1)]
    [Arguments(132, 92, BloodPressureCategory.Grade1)]
    [Arguments(159, 99, BloodPressureCategory.Grade1)]
    [Arguments(160, 90, BloodPressureCategory.Grade2)]
    [Arguments(150, 100, BloodPressureCategory.Grade2)]
    [Arguments(179, 109, BloodPressureCategory.Grade2)]
    [Arguments(180, 100, BloodPressureCategory.Grade3)]
    [Arguments(150, 110, BloodPressureCategory.Grade3)]
    [Arguments(185, 115, BloodPressureCategory.Grade3)]
    public async Task Classify_UsesTheHigherOfSystolicAndDiastolicCategory(int systolic, int diastolic, BloodPressureCategory expected)
    {
        await Assert.That(BloodPressureClassifier.Classify(systolic, diastolic).Category).IsEqualTo(expected);
    }

    [Test]
    [Arguments(140, 89, true)]
    [Arguments(165, 70, true)]
    [Arguments(140, 90, false)]
    [Arguments(139, 70, false)]
    [Arguments(118, 76, false)]
    public async Task Classify_FlagsIsolatedSystolicHypertension(int systolic, int diastolic, bool expected)
    {
        await Assert.That(BloodPressureClassifier.Classify(systolic, diastolic).IsIsolatedSystolic).IsEqualTo(expected);
    }
}
