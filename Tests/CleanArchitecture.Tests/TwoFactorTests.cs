using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace CleanArchitecture.Tests;

/// <summary>Phase 2 der ADR 0006: Zwei-Faktor-Anmeldung (TOTP) mit Wiederherstellungscodes, gegen echte Identity und PostgreSQL.</summary>
[Category("Integration")]
[ClassDataSource<PostgresFixture>(Shared = SharedType.PerTestSession)]
public class TwoFactorTests(PostgresFixture postgres)
{
    // Zur Laufzeit erzeugt, kein festes Passwort im Quelltext.
    private static readonly string Password = $"Pw{Guid.NewGuid():N}!aA1";
    private const string Email = "mfa@example.test";

    /// <summary>TOTP nach RFC 6238 (HMAC-SHA1, 30 Sekunden, 6 Stellen) aus dem Base32-Schlüssel der API.</summary>
    private static string TotpCode(string base32Key, DateTimeOffset now)
    {
        var key = Base32Decode(base32Key);
        var counter = (long)Math.Floor(now.ToUnixTimeSeconds() / 30d);
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        var hash = HMACSHA1.HashData(key, counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var buffer = 0;
        var result = new List<byte>();
        foreach (var c in value.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                result.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. result];
    }

    private async Task<ApiFactory> StartAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using (var context = PostgresFixture.CreateContext(connectionString))
        {
            await context.Database.MigrateAsync();
        }

        return new ApiFactory(connectionString);
    }

    private static async Task<(string AccessToken, string RefreshToken)> RegisterAndLoginAsync(HttpClient http)
    {
        (await http.PostAsJsonAsync("/api/auth/register", new { email = Email, password = Password })).EnsureSuccessStatusCode();
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
        login.EnsureSuccessStatusCode();
        var tokens = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
        return (tokens.GetProperty("accessToken").GetString()!, tokens.GetProperty("refreshToken").GetString()!);
    }

    private static async Task<JsonElement> ManageAsync(HttpClient http, string accessToken, object body, bool expectSuccess = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/manage/2fa") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await http.SendAsync(request);
        if (expectSuccess)
        {
            response.EnsureSuccessStatusCode();
        }

        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrEmpty(text) ? "{}" : text).RootElement.Clone();
    }

    /// <summary>Schaltet 2FA ein und liefert Schlüssel und Wiederherstellungscodes.</summary>
    private static async Task<(string SharedKey, string[] RecoveryCodes)> EnableAsync(HttpClient http, string accessToken)
    {
        var status = await ManageAsync(http, accessToken, new { });
        var key = status.GetProperty("sharedKey").GetString()!;
        var enabled = await ManageAsync(http, accessToken, new { enable = true, twoFactorCode = TotpCode(key, DateTimeOffset.UtcNow) });
        var codes = enabled.GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        return (key, codes);
    }

    [Test]
    public async Task Status_BeforeEnabling_OffersASharedKeyAndNoCodes()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);

        var status = await ManageAsync(http, access, new { });

        await Assert.That(status.GetProperty("isTwoFactorEnabled").GetBoolean()).IsFalse();
        await Assert.That(status.GetProperty("sharedKey").GetString()).IsNotNull().And.IsNotEmpty();
        await Assert.That(status.GetProperty("recoveryCodesLeft").GetInt32()).IsEqualTo(0);
    }

    [Test]
    public async Task Enable_WithAnInvalidCode_IsRejectedAndStaysOff()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        _ = await ManageAsync(http, access, new { });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/manage/2fa") { Content = JsonContent.Create(new { enable = true, twoFactorCode = "000000" }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var response = await http.SendAsync(request);
        var status = await ManageAsync(http, access, new { });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(status.GetProperty("isTwoFactorEnabled").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task Enable_WithAValidCode_ReturnsTenRecoveryCodes()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);

        var (_, codes) = await EnableAsync(http, access);
        var status = await ManageAsync(http, access, new { });

        await Assert.That(codes.Length).IsEqualTo(10);
        await Assert.That(status.GetProperty("isTwoFactorEnabled").GetBoolean()).IsTrue();
        await Assert.That(status.GetProperty("recoveryCodesLeft").GetInt32()).IsEqualTo(10);
        await Assert.That(status.TryGetProperty("recoveryCodes", out var again) && again.ValueKind == JsonValueKind.Array).IsFalse();
    }

    [Test]
    public async Task Login_WithTwoFactorOn_RequiresACode()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        var (key, _) = await EnableAsync(http, access);

        var withoutCode = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });
        var wrongCode = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password, twoFactorCode = "000000" });
        var rightCode = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password, twoFactorCode = TotpCode(key, DateTimeOffset.UtcNow) });

        await Assert.That(withoutCode.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await withoutCode.Content.ReadAsStringAsync()).Contains("RequiresTwoFactor");
        await Assert.That(wrongCode.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await wrongCode.Content.ReadAsStringAsync()).Contains("Failed");
        await Assert.That(rightCode.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Login_WithAWrongPassword_DoesNotRevealThatTwoFactorIsOn()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        _ = await EnableAsync(http, access);

        var response = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = "falsch" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await response.Content.ReadAsStringAsync()).DoesNotContain("RequiresTwoFactor");
    }

    [Test]
    public async Task RecoveryCode_WorksOnce()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        var (_, codes) = await EnableAsync(http, access);

        var first = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password, twoFactorRecoveryCode = codes[0] });
        var second = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password, twoFactorRecoveryCode = codes[0] });
        var status = await ManageAsync(http, access, new { });

        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(status.GetProperty("recoveryCodesLeft").GetInt32()).IsEqualTo(9);
    }

    [Test]
    public async Task Disable_AllowsLoginWithoutACodeAgain()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        var (key, _) = await EnableAsync(http, access);

        var disabled = await ManageAsync(http, access, new { enable = false, twoFactorCode = TotpCode(key, DateTimeOffset.UtcNow) });
        var login = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password });

        await Assert.That(disabled.GetProperty("isTwoFactorEnabled").GetBoolean()).IsFalse();
        await Assert.That(login.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ResetRecoveryCodes_ReturnsNewCodesAndInvalidatesTheOldOnes()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        var (key, oldCodes) = await EnableAsync(http, access);

        var reset = await ManageAsync(http, access, new { resetRecoveryCodes = true, twoFactorCode = TotpCode(key, DateTimeOffset.UtcNow) });
        var newCodes = reset.GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        var withOld = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password, twoFactorRecoveryCode = oldCodes[0] });
        var withNew = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password, twoFactorRecoveryCode = newCodes[0] });

        await Assert.That(newCodes.Length).IsEqualTo(10);
        await Assert.That(newCodes.Intersect(oldCodes)).IsEmpty();
        await Assert.That(withOld.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(withNew.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Refresh_AfterEnablingTwoFactor_IsRejectedForOldTokens()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, refresh) = await RegisterAndLoginAsync(http);
        _ = await EnableAsync(http, access);

        var response = await http.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = refresh });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Disable_WithoutACode_IsRejectedAndKeepsTwoFactorOn()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        _ = await EnableAsync(http, access);

        var withoutCode = await ManageAsync(http, access, new { enable = false }, expectSuccess: false);
        var wrongCode = await ManageAsync(http, access, new { enable = false, twoFactorCode = "000000" }, expectSuccess: false);
        var status = await ManageAsync(http, access, new { });

        await Assert.That(withoutCode.ToString()).Contains("twoFactorCode");
        await Assert.That(wrongCode.ToString()).Contains("twoFactorCode");
        await Assert.That(status.GetProperty("isTwoFactorEnabled").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ResetRecoveryCodes_WithoutACode_IsRejected()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        var (_, codes) = await EnableAsync(http, access);

        var rejected = await ManageAsync(http, access, new { resetRecoveryCodes = true }, expectSuccess: false);
        var stillWorks = await http.PostAsJsonAsync("/api/auth/login", new { email = Email, password = Password, twoFactorRecoveryCode = codes[0] });

        await Assert.That(rejected.ToString()).Contains("twoFactorCode");
        await Assert.That(stillWorks.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ResetSharedKey_BeforeEnabling_NeedsNoCode()
    {
        await using var factory = await StartAsync();
        var http = factory.CreateClient();
        var (access, _) = await RegisterAndLoginAsync(http);
        var first = await ManageAsync(http, access, new { });

        var second = await ManageAsync(http, access, new { resetSharedKey = true });

        await Assert.That(second.GetProperty("sharedKey").GetString()).IsNotEqualTo(first.GetProperty("sharedKey").GetString());
    }

    [Test]
    public async Task ManageTwoFactor_WithoutToken_Returns401()
    {
        await using var factory = await StartAsync();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/manage/2fa", new { });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}
