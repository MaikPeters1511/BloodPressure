namespace CleanArchitecture.Contracts;

/// <param name="Category">Einordnung nach ESC/ESH 2018 (<c>Optimal</c>, <c>Normal</c>, <c>HighNormal</c>, <c>Grade1</c>–<c>Grade3</c>); keine Diagnose.</param>
public sealed record BloodPressureDto(int Id, int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen, string Category);
