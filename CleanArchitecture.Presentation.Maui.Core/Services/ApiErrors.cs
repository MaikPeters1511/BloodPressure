using Refit;

namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Liest Feldmeldungen aus 400-Antworten (ValidationProblemDetails), ohne technische Details nach außen zu geben.</summary>
public static class ApiErrors
{
    private sealed record Problem(Dictionary<string, string[]>? Errors);

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
