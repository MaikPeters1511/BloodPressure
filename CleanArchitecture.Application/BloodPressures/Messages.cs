namespace CleanArchitecture.Application.BloodPressures;

// Queries
public sealed record GetBloodPressures;
public sealed record GetBloodPressureById(int Id);

// Commands
public sealed record CreateBloodPressure(int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen);
public sealed record UpdateBloodPressure(int Id, int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen);
public sealed record DeleteBloodPressure(int Id);
