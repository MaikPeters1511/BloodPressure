namespace CleanArchitecture.Presentation.Maui.Core.Services;

/// <summary>Plattformeigener sicherer Speicher (Keychain/Keystore); hier liegen ausschließlich Tokens.</summary>
public interface ISecureStore
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    Task RemoveAsync(string key);
}
