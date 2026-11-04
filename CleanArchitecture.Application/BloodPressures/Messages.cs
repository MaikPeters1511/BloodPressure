using CleanArchitecture.Contracts;

namespace CleanArchitecture.Application.BloodPressures;

// Alle Messages tragen die UserId des angemeldeten Nutzers (aus dem Token, nie aus dem Request).

// Queries
public sealed record GetBloodPressures(string UserId, BloodPressureListQuery Query);
public sealed record GetStatistics(string UserId, StatisticsQuery Query);
public sealed record GetBloodPressureById(string UserId, int Id);

// Commands
public sealed record CreateBloodPressure(string UserId, int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen);
public sealed record UpdateBloodPressure(string UserId, int Id, int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen);
public sealed record DeleteBloodPressure(string UserId, int Id);
