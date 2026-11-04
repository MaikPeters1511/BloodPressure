using Microsoft.JSInterop;

namespace CleanArchitecture.Presentation.UI.Services;

/// <summary>
/// Speichert in <c>sessionStorage</c>: die Tokens verfallen mit dem Tab. Sie sind dennoch für JavaScript der Seite
/// lesbar (XSS); ein BFF mit Cookies wäre sicherer, ist aber eine größere Architekturänderung.
/// </summary>
public sealed class SessionTokenStorage(IJSRuntime js) : ITokenStorage
{
    public async Task<string?> GetAsync(string key) => await js.InvokeAsync<string?>("sessionStorage.getItem", key);

    public async Task SetAsync(string key, string value) => await js.InvokeVoidAsync("sessionStorage.setItem", key, value);

    public async Task RemoveAsync(string key) => await js.InvokeVoidAsync("sessionStorage.removeItem", key);
}
