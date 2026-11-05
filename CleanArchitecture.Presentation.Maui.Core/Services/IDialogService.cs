namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Bestätigungsdialoge und Screenreader-Ansagen (Implementierung im MAUI-Projekt).</summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Sagt eine Statusänderung für TalkBack/VoiceOver an (<c>SemanticScreenReader.Announce</c>).</summary>
    void Announce(string message);
}
