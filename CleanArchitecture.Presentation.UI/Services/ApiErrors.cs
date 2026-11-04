using Refit;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Liest Fehlermeldungen aus API-Antworten, ohne technische Details nach außen zu geben.</summary>
public static class ApiErrors
{
    private sealed record Problem(Dictionary<string, string[]>? Errors);

    /// <summary>Alle Feldmeldungen einer 400-Antwort (ValidationProblemDetails); leer, wenn es keine gibt.</summary>
    public static async Task<IReadOnlyList<string>> ValidationMessagesAsync(ApiException exception)
    {
        try
        {
            var problem = await exception.GetContentAsAsync<Problem>();
            return problem?.Errors?.Values.SelectMany(messages => messages).Distinct().ToList() ?? [];
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ApiException or InvalidOperationException)
        {
            return [];
        }
    }
}
