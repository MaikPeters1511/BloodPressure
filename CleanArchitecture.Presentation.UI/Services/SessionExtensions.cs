using Microsoft.AspNetCore.Components;

namespace CleanArchitecture.Presentation.UI.Services;

public static class SessionExtensions
{
    /// <summary>Meldet ab und leitet auf die Anmeldeseite (bei 401 oder fehlender Anmeldung).</summary>
    public static async Task SignOutToLoginAsync(this AuthSession session, NavigationManager navigation)
    {
        await session.LogoutAsync();
        navigation.NavigateTo("login");
    }
}
