using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CleanArchitecture.Presentation.API.Infrastructure;

/// <summary>
/// Ergänzt in der OpenAPI-Beschreibung Beispiele für Fehlerantworten (RFC 9457, application/problem+json),
/// damit Clients das Format kennen: 400 (Validierung), 401, 404, 412 und 429.
/// </summary>
public static class ProblemExamplesTransformer
{
    private const string ProblemJson = "application/problem+json";

    public static Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var path = context.Description.RelativePath ?? string.Empty;
        if (!path.StartsWith("api/", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var method = context.Description.HttpMethod ?? string.Empty;
        var hasInput = operation.RequestBody is not null || (operation.Parameters?.Count ?? 0) > 0;

        if (hasInput)
        {
            Add(operation, "400", "Ungültige Eingabe", new JsonObject
            {
                ["title"] = "One or more validation errors occurred.",
                ["status"] = 400,
                ["errors"] = new JsonObject { ["systolisch"] = new JsonArray("Systolisch muss zwischen 50 und 300 liegen.") }
            });
        }

        if (!path.StartsWith("api/auth", StringComparison.OrdinalIgnoreCase))
        {
            Add(operation, "401", "Nicht angemeldet", new JsonObject { ["title"] = "Unauthorized", ["status"] = 401 });
        }

        if (path.Contains('{'))
        {
            Add(operation, "404", "Nicht gefunden", new JsonObject { ["title"] = "Not Found", ["status"] = 404 });
        }

        if (method is "PUT" or "DELETE")
        {
            Add(operation, "412", "Veralteter Stand (If-Match)", new JsonObject
            {
                ["title"] = "Veralteter Stand",
                ["status"] = 412,
                ["detail"] = "Die Messung wurde zwischenzeitlich geändert."
            });
        }

        Add(operation, "429", "Zu viele Anfragen", new JsonObject { ["title"] = "Too Many Requests", ["status"] = 429 });
        return Task.CompletedTask;
    }

    private static void Add(OpenApiOperation operation, string status, string description, JsonObject example)
    {
        operation.Responses ??= new OpenApiResponses();
        if (operation.Responses.ContainsKey(status))
        {
            return;
        }

        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemJson] = new() { Example = example }
            }
        };
    }
}
