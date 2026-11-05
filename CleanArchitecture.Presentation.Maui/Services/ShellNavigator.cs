using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Presentation.Maui.Services;

public sealed class ShellNavigator : INavigator
{
    public Task GoToLoginAsync() => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//login"));

    public Task GoToMeasurementsAsync() => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//measurements"));

    public Task GoToProfileAsync() => MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("profile"));
}
