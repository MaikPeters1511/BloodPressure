using System.Text.Json.Serialization;

namespace CleanArchitecture.Contracts;

/// <param name="Version">Zeilenversion für Optimistic Concurrency; als <c>ETag</c> in <c>GET</c>-Antworten.</param>
/// <param name="Category">Einordnung nach ESC 2024 (<c>NonElevated</c>, <c>Elevated</c>, <c>Hypertension</c>); keine Diagnose.</param>
public sealed record BloodPressureDto(int Id, int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen, string Category, uint Version)
{
    /// <summary>Wert für den <c>If-Match</c>-Header bei <c>PUT</c>/<c>DELETE</c> (entspricht dem <c>ETag</c>-Header von <c>GET</c>).</summary>
    [JsonIgnore]
    public string ETag => $"\"{Version}\"";
}
