using System.Net.Http.Json;

namespace CleanArchitecture.Presentation.UI.Services;

public interface I[Name]ApiClient
{
    Task<IReadOnlyList<[Name]Dto>> GetAllAsync(CancellationToken cancellationToken = default);
}

// Typisierter Client: HttpClient wird per DI (AddHttpClient<I[Name]ApiClient, [Name]ApiClient>) injiziert.
public sealed class [Name]ApiClient(HttpClient httpClient) : I[Name]ApiClient
{
    public async Task<IReadOnlyList<[Name]Dto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<List<[Name]Dto>>("api/[name]", cancellationToken);
        return result ?? [];
    }
}
