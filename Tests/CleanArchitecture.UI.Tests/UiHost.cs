using System.Globalization;
using Bunit;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CleanArchitecture.UI.Tests;

/// <summary>Baut einen bUnit-Kontext mit Fakes für API, Tokenspeicher und Zeit.</summary>
public sealed class UiHost : IDisposable
{
    public BunitContext Context { get; } = new();
    public FakeTokenStorage Storage { get; } = new();
    public FakeAuthApi Auth { get; } = new();
    public FakeBloodPressureApi Api { get; } = new();
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    public AuthSession Session { get; }

    public UiHost(string culture = "de")
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        Session = new AuthSession(Storage, Auth, Time);
        Context.Services.AddLogging();
        Context.Services.AddLocalization();
        Context.Services.AddSingleton<ITokenStorage>(Storage);
        Context.Services.AddSingleton<IAuthApi>(Auth);
        Context.Services.AddSingleton<IBloodPressureApi>(Api);
        Context.Services.AddSingleton<TimeProvider>(Time);
        Context.Services.AddSingleton(Session);
    }

    public string CurrentPath => new Uri(Context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).AbsolutePath;

    public Task SignInAsync() => Session.LoginAsync("a@example.test", "pw");

    public void Dispose() => Context.Dispose();
}
