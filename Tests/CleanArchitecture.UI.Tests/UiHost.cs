using System.Globalization;
using Bunit;
using CleanArchitecture.Contracts;
using CleanArchitecture.Presentation.UI.Services;
using CleanArchitecture.Presentation.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Time.Testing;

namespace CleanArchitecture.UI.Tests;

/// <summary>Baut einen bUnit-Kontext mit Fakes für API, Tokenspeicher und Zeit.</summary>
public sealed class UiHost : IDisposable
{
    public BunitContext Context { get; } = new();
    public FakeTokenStorage Storage { get; } = new();
    public FakeAuthApi Auth { get; } = new();
    public FakeBloodPressureApi Api { get; } = new();
    public FakeReminderApi Reminders { get; } = new();
    public FakeNotificationApi Notifications { get; } = new();
    public FakeBrowser Browser { get; } = new();
    public FakeProfileApi Profile { get; } = new();
    public FakeTwoFactorBackend TwoFactor => Reminders.TwoFactor;
    public FakeTheme Theme { get; } = new();
    public FakeLanguage Language { get; }
    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    public AuthSession Session { get; }

    public UiHost(string culture = "de")
    {
        Language = new FakeLanguage(culture);
        Session = new AuthSession(Storage, Auth, Time);
        Context.Services.AddLogging();
        Context.Services.AddSingleton<IStringLocalizer<SharedResource>>(new FixedCultureLocalizer(() => new CultureInfo(Language.Current)));
        Context.Services.AddSingleton<ITokenStorage>(Storage);
        Context.Services.AddSingleton<IAuthApi>(Auth);
        Context.Services.AddSingleton<IBloodPressureApi>(Api);
        Context.Services.AddSingleton<IReminderApi>(Reminders);
        Context.Services.AddSingleton<INotificationApi>(Notifications);
        Context.Services.AddSingleton<IBrowserInterop>(Browser);
        Context.Services.AddSingleton<IProfileApi>(Profile);
        Context.Services.AddSingleton<IThemeService>(Theme);
        Context.Services.AddSingleton<ILanguageService>(Language);
        Context.Services.AddSingleton<NotificationCounter>();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.Services.AddSingleton<TimeProvider>(Time);
        Context.Services.AddSingleton(Session);
    }

    public string CurrentPath => new Uri(Context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).AbsolutePath;

    public Task SignInAsync() => Session.LoginAsync("a@example.test", "pw");

    public void Dispose() => Context.Dispose();
}
