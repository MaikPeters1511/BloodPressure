using CleanArchitecture.Presentation.Maui.Core.Services;

namespace CleanArchitecture.Presentation.Maui.Services;

/// <summary>Legt Tokens im Keychain (iOS/macOS) bzw. Keystore (Android) ab.</summary>
public sealed class MauiSecureStore : ISecureStore
{
    public Task<string?> GetAsync(string key) => SecureStorage.Default.GetAsync(key);

    public Task SetAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);

    public Task RemoveAsync(string key)
    {
        SecureStorage.Default.Remove(key);
        return Task.CompletedTask;
    }
}
