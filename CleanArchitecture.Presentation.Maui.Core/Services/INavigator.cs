namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Seitenwechsel, plattformunabhängig abstrahiert (Implementierung im MAUI-Projekt über Shell).</summary>
public interface INavigator
{
    Task GoToLoginAsync();

    Task GoToMeasurementsAsync();
}
