using Refit;

namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Liest Feldmeldungen aus 400-Antworten (ValidationProblemDetails), ohne technische Details nach außen zu geben.</summary>
public static class ApiErrors
{
    private sealed record Problem(Dictionary<string, string[]>? Errors, string? Detail);

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

    /// <summary>
    /// <c>true</c>, wenn die Login-Antwort (401) sagt, dass die E-Mail-Adresse noch nicht bestätigt ist
    /// (ProblemDetails <c>detail</c> = "NotAllowed"). Alles andere ("Failed") heißt: Zugangsdaten falsch.
    /// </summary>
    public static async Task<bool> IsEmailNotConfirmedAsync(ApiException exception)
    {
        try
        {
            var problem = await exception.GetContentAsAsync<Problem>();
            return string.Equals(problem?.Detail, "NotAllowed", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or ApiException or InvalidOperationException)
        {
            return false;
        }
    }
}
