using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Presentation.Maui.Services;

public sealed class MauiLauncherService : ILauncherService
{
    // TryOpenAsync braucht keine Paketsichtbarkeit (Android 11+) und liefert false, wenn keine App zuständig ist.
    public Task<bool> OpenAsync(Uri uri) => MainThread.InvokeOnMainThreadAsync(() => Launcher.Default.TryOpenAsync(uri));
}
