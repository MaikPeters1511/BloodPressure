using System.Net;
using System.Text;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI.Services;
using Refit;

namespace CleanArchitecture.UI.Tests;

public sealed class FakeTokenStorage : ITokenStorage
{
    public Dictionary<string, string> Items { get; } = [];

    public Task<string?> GetAsync(string key) => Task.FromResult(Items.GetValueOrDefault(key));

    public Task SetAsync(string key, string value)
    {
        Items[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        Items.Remove(key);
        return Task.CompletedTask;
    }
}

public sealed class FakeAuthApi : IAuthApi
{
    public Func<CredentialsRequest, Task<TokenResponse>>? OnLogin { get; set; }
    public Func<CredentialsRequest, Task>? OnRegister { get; set; }
    public Func<RefreshRequest, Task<TokenResponse>>? OnRefresh { get; set; }
    public List<string> Calls { get; } = [];

    public Task RegisterAsync(CredentialsRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add("register");
        return OnRegister?.Invoke(request) ?? Task.CompletedTask;
    }

    public Task<TokenResponse> LoginAsync(CredentialsRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add("login");
        return OnLogin?.Invoke(request) ?? Task.FromResult(Tokens("access-1", "refresh-1"));
    }

    public Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add("refresh");
        return OnRefresh?.Invoke(request) ?? Task.FromResult(Tokens("access-2", "refresh-2"));
    }

    public static TokenResponse Tokens(string access, string refresh, int expiresIn = 3600) => new("Bearer", access, expiresIn, refresh);
}

public sealed class FakeBloodPressureApi : IBloodPressureApi
{
    public List<BloodPressureDto> Items { get; } = [];
    public Exception? FailWith { get; set; }
    public Exception? DeleteFailWith { get; set; }
    public Exception? CreateFailWith { get; set; }
    public List<(int? Page, int? PageSize, string? Sort)> ListCalls { get; } = [];
    public List<BloodPressureRequest> Created { get; } = [];
    public List<(int Id, string? IfMatch)> Deleted { get; } = [];

    public Task<PagedResult<BloodPressureDto>> GetAllAsync(
        DateTime? from = null, DateTime? to = null, int? page = null, int? pageSize = null, string? sort = null,
        CancellationToken cancellationToken = default)
    {
        ListCalls.Add((page, pageSize, sort));
        if (FailWith is not null)
        {
            return Task.FromException<PagedResult<BloodPressureDto>>(FailWith);
        }

        var size = pageSize ?? 50;
        var current = page ?? 1;
        var ordered = Items.OrderByDescending(i => i.DateTime).ToList();
        return Task.FromResult(new PagedResult<BloodPressureDto>(
            ordered.Skip((current - 1) * size).Take(size).ToList(), current, size, ordered.Count));
    }

    public Task<BloodPressureDto> CreateAsync(BloodPressureRequest request, CancellationToken cancellationToken = default)
    {
        if (CreateFailWith is not null)
        {
            return Task.FromException<BloodPressureDto>(CreateFailWith);
        }

        Created.Add(request);
        var dto = Make(Items.Count + 1, request.Systolisch, request.Diastolisch, request.DateTime, request.Pulse, request.Oxygen);
        Items.Add(dto);
        return Task.FromResult(dto);
    }

    public Task DeleteAsync(int id, string? ifMatch = null, CancellationToken cancellationToken = default)
    {
        Deleted.Add((id, ifMatch));
        if (DeleteFailWith is not null)
        {
            return Task.FromException(DeleteFailWith);
        }

        Items.RemoveAll(i => i.Id == id);
        return Task.CompletedTask;
    }

    public Task<BloodPressureDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task UpdateAsync(int id, BloodPressureRequest request, string? ifMatch = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<StatisticsBucketDto>> GetStatisticsAsync(DateTime? from = null, DateTime? to = null, string? interval = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Stream> ExportAsync(string? format = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public static BloodPressureDto Make(int id, int sys = 120, int dia = 80, DateTime? at = null, int pulse = 65, int oxygen = 98, string category = "Normal", uint version = 7)
        => new(id, sys, dia, at ?? new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc).AddMinutes(id), pulse, oxygen, category, version);

    public void Seed(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            Items.Add(Make(i));
        }
    }
}

/// <summary>Erzeugt echte Refit-<see cref="ApiException"/>s wie sie die API-Clients werfen.</summary>
public static class ApiExceptions
{
    public static ApiException Create(HttpStatusCode status, string? json = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://api.test/x");
        var response = new HttpResponseMessage(status)
        {
            RequestMessage = request,
            Content = new StringContent(json ?? "", Encoding.UTF8, json is null ? "text/plain" : "application/problem+json")
        };
        return ApiException.Create(request, HttpMethod.Get, response, new RefitSettings()).GetAwaiter().GetResult();
    }

    public static ApiException Validation(string field, string message)
        => Create(HttpStatusCode.BadRequest, System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Ungültige Eingabe",
            status = 400,
            errors = new Dictionary<string, string[]> { [field] = [message] }
        }));
}
