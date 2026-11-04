namespace CleanArchitecture.Contracts;

public sealed record BloodPressureRequest(int Systolisch, int Diastolisch, DateTime DateTime, int Pulse, int Oxygen);
