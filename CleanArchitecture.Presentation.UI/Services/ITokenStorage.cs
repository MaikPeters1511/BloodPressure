namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>Ablage der Tokens im Browser. Abstraktion, damit Tests ohne JavaScript laufen.</summary>
public interface ITokenStorage
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
    Task RemoveAsync(string key);
}
