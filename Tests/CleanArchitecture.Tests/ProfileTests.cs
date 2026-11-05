using System.Net;
using CleanArchitecture.Application.Profiles;
using CleanArchitecture.Contracts;
using CleanArchitecture.Domain.Entities;
using Refit;

namespace CleanArchitecture.Tests;

[Category("Unit")]
public class UserProfileDomainTests
{
    private static readonly DateTime Now2026 = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Apply_StoresBirthYearInsteadOfAge()
    {
        var profile = new UserProfile { UserId = "u1" };

        profile.Apply(42, 180, 82.5m, Now2026);

        await Assert.That(profile.BirthYear).IsEqualTo(1984);
        await Assert.That(profile.HeightCm).IsEqualTo(180);
        await Assert.That(profile.WeightKg).IsEqualTo(82.5m);
    }

    [Test]
    public async Task Age_StaysCorrectOverTheYears()
    {
        var profile = new UserProfile { UserId = "u1" };
        profile.Apply(42, null, null, Now2026);

        await Assert.That(profile.AgeAt(Now2026)).IsEqualTo(42);
        await Assert.That(profile.AgeAt(new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc))).IsEqualTo(43);
    }

    [Test]
    public async Task Apply_WithNulls_ClearsTheValues()
    {
        var profile = new UserProfile { UserId = "u1" };
        profile.Apply(42, 180, 82.5m, Now2026);

        profile.Apply(null, 180, null, Now2026);

        await Assert.That(profile.AgeAt(Now2026)).IsNull();
        await Assert.That(profile.WeightKg).IsNull();
        await Assert.That(profile.HeightCm).IsEqualTo(180);
    }

    [Test]
    [Arguments(-1, 180, 80.0)]
    [Arguments(121, 180, 80.0)]
    [Arguments(40, 49, 80.0)]
    [Arguments(40, 251, 80.0)]
    [Arguments(40, 180, 1.9)]
    [Arguments(40, 180, 500.1)]
    public async Task Apply_OutOfRange_Throws(int age, int height, double weight)
    {
        var profile = new UserProfile { UserId = "u1" };

        await Assert.That(() => profile.Apply(age, height, (decimal)weight, Now2026)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task IsEmpty_WhenNothingIsSet()
    {
        var profile = new UserProfile { UserId = "u1" };

        await Assert.That(profile.IsEmpty).IsTrue();
        profile.Apply(30, null, null, Now2026);
        await Assert.That(profile.IsEmpty).IsFalse();
    }
}

[Category("Unit")]
public class ProfileValidatorTests
{
    private readonly ProfileValidator _validator = new();

    [Test]
    [Arguments(0, 50, 2.0, true)]
    [Arguments(120, 250, 500.0, true)]
    [Arguments(121, 180, 80.0, false)]
    [Arguments(40, 49, 80.0, false)]
    [Arguments(40, 180, 80.55, false)]
    [Arguments(40, 180, 1.0, false)]
    public async Task Validate_Boundaries(int age, int height, double weight, bool valid)
    {
        var result = await _validator.ValidateAsync(new ProfileDto(age, height, (decimal)weight));

        await Assert.That(result.IsValid).IsEqualTo(valid);
    }

    [Test]
    public async Task AllEmpty_IsValid()
        => await Assert.That((await _validator.ValidateAsync(new ProfileDto(null, null, null))).IsValid).IsTrue();

    [Test]
    public async Task ErrorMessages_NameTheFieldsInGerman()
    {
        var result = await _validator.ValidateAsync(new ProfileDto(200, 20, 600m));

        await Assert.That(result.Errors.Select(e => e.PropertyName)).IsEquivalentTo(new[] { "Age", "HeightCm", "WeightKg" });
        await Assert.That(result.Errors.All(e => e.ErrorMessage.Contains("muss"))).IsTrue();
    }
}

[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class ProfileApiTests(PostgresFixture postgres)
{
    [Test]
    public async Task NoProfile_ReturnsEmptyProfile_Not404()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IProfileApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));

        var profile = await api.GetAsync();

        await Assert.That(profile).IsEqualTo(new ProfileDto(null, null, null));
    }

    [Test]
    public async Task PutThenGet_ReturnsStoredValues()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IProfileApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));

        await api.PutAsync(new ProfileDto(42, 180, 82.5m));
        var profile = await api.GetAsync();

        await Assert.That(profile).IsEqualTo(new ProfileDto(42, 180, 82.5m));
    }

    [Test]
    public async Task Put_Replaces_AndClearsOmittedValues()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IProfileApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));
        await api.PutAsync(new ProfileDto(42, 180, 82.5m));

        await api.PutAsync(new ProfileDto(42, 180, null));

        await Assert.That(await api.GetAsync()).IsEqualTo(new ProfileDto(42, 180, null));
    }

    [Test]
    public async Task InvalidHeight_Returns400_WithCamelCaseFieldName_AndKeepsOldData()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IProfileApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));
        await api.PutAsync(new ProfileDto(42, 180, 82.5m));

        var ex = await Assert.That(async () => await api.PutAsync(new ProfileDto(42, 20, 82.5m))).Throws<ApiException>();

        await Assert.That(ex!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var problem = await ex.GetContentAsAsync<ProblemWithErrors>();
        await Assert.That(problem!.Errors.Keys).Contains("heightCm");
        await Assert.That((await api.GetAsync()).HeightCm).IsEqualTo(180);
    }

    [Test]
    public async Task Delete_RemovesAllValues()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var api = RestService.For<IProfileApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));
        await api.PutAsync(new ProfileDto(42, 180, 82.5m));

        await api.DeleteAsync();

        await Assert.That(await api.GetAsync()).IsEqualTo(new ProfileDto(null, null, null));
    }

    [Test]
    public async Task Profiles_AreSeparatedPerUser()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var a = RestService.For<IProfileApi>(await RealApiHarness.SignInAsync(factory, "a@example.test"));
        var b = RestService.For<IProfileApi>(await RealApiHarness.SignInAsync(factory, "b@example.test"));

        await a.PutAsync(new ProfileDto(42, 180, 82.5m));

        await Assert.That(await b.GetAsync()).IsEqualTo(new ProfileDto(null, null, null));
    }

    [Test]
    public async Task WithoutSignIn_Returns401()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var response = await factory.CreateClient().GetAsync("/api/v1/profile");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Export_ContainsNoProfileData()
    {
        await using var factory = await RealApiHarness.StartAsync(postgres);
        var http = await RealApiHarness.SignInAsync(factory, "a@example.test");
        await RestService.For<IProfileApi>(http).PutAsync(new ProfileDto(42, 183, 82.5m));
        await RestService.For<IBloodPressureApi>(http).CreateAsync(new BloodPressureRequest(120, 80, new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), 65, 98));

        var csv = await (await http.GetAsync("/api/v1/BloodPressure/export?format=csv")).Content.ReadAsStringAsync();

        await Assert.That(csv).DoesNotContain("183");
        await Assert.That(csv).DoesNotContain("82,5");
        await Assert.That(csv).DoesNotContain("82.5");
        await Assert.That(csv).DoesNotContain("1984");
    }

    private sealed record ProblemWithErrors(Dictionary<string, string[]> Errors);
}

[Category("Unit")]
public class ProfileCalculatorTests
{
    [Test]
    [Arguments(180, 82.5, 25.5)]
    [Arguments(170, 70.0, 24.2)]
    [Arguments(160, 50.0, 19.5)]
    [Arguments(200, 100.0, 25.0)]
    public async Task Bmi_IsWeightOverHeightSquared_RoundedToOneDecimal(int height, double weight, double expected)
        => await Assert.That(ProfileCalculator.Bmi(height, (decimal)weight)).IsEqualTo((decimal)expected);

    [Test]
    [Arguments(null, 80.0)]
    public async Task Bmi_MissingHeight_IsNull(int? height, double weight)
        => await Assert.That(ProfileCalculator.Bmi(height, (decimal)weight)).IsNull();

    [Test]
    public async Task Bmi_MissingWeight_IsNull()
        => await Assert.That(ProfileCalculator.Bmi(180, null)).IsNull();
}
