using CleanArchitecture.Domain.Entities;

namespace CleanArchitecture.Tests;

[Category("Unit")]
public class ClassifierTests
{
    [Test]
    [Arguments(118, 68, BloodPressureCategory.NonElevated)]
    [Arguments(119, 69, BloodPressureCategory.NonElevated)]
    [Arguments(120, 69, BloodPressureCategory.Elevated)]
    [Arguments(119, 70, BloodPressureCategory.Elevated)]
    [Arguments(139, 89, BloodPressureCategory.Elevated)]
    [Arguments(130, 80, BloodPressureCategory.Elevated)]
    [Arguments(140, 85, BloodPressureCategory.Hypertension)]
    [Arguments(132, 90, BloodPressureCategory.Hypertension)]
    [Arguments(118, 95, BloodPressureCategory.Hypertension)]
    [Arguments(160, 100, BloodPressureCategory.Hypertension)]
    [Arguments(185, 115, BloodPressureCategory.Hypertension)]
    public async Task Classify_UsesTheHigherOfSystolicAndDiastolicCategory(int systolic, int diastolic, BloodPressureCategory expected)
    {
        await Assert.That(BloodPressureClassifier.Classify(systolic, diastolic).Category).IsEqualTo(expected);
    }

    [Test]
    [Arguments(140, 89, true)]
    [Arguments(165, 70, true)]
    [Arguments(140, 90, false)]
    [Arguments(139, 70, false)]
    [Arguments(118, 68, false)]
    public async Task Classify_FlagsIsolatedSystolicHypertension(int systolic, int diastolic, bool expected)
    {
        await Assert.That(BloodPressureClassifier.Classify(systolic, diastolic).IsIsolatedSystolic).IsEqualTo(expected);
    }

    [Test]
    [Arguments(179, 109, false)]
    [Arguments(180, 80, true)]
    [Arguments(150, 110, true)]
    [Arguments(185, 115, true)]
    [Arguments(140, 90, false)]
    public async Task IsSevere_StartsAt180Or110(int systolic, int diastolic, bool expected)
    {
        await Assert.That(BloodPressureClassifier.IsSevere(systolic, diastolic)).IsEqualTo(expected);
    }
}
