namespace CleanArchitecture.Presentation.Maui.Core.ViewModels;

/// <summary>Grobe Einordnung für die Farbmarkierung. Die Information steht immer zusätzlich als Text, Farbe allein genügt nicht.</summary>
public enum CategoryLevel { Unknown, Normal, Elevated, Warning, High }

/// <summary>Eine Zeile der Messwertliste, fertig lokalisiert; <see cref="Summary"/> ist die vollständige Screenreader-Ansage.</summary>
public sealed record MeasurementItem(
    int Id, int Systolic, int Diastolic, int Pulse, int Oxygen, string DateText,
    string CategoryText, CategoryLevel CategoryLevel, string ETag, string Summary);
