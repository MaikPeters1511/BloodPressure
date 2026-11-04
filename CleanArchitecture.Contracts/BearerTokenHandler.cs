using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace CleanArchitecture.Contracts;

/// <summary>Sendet das Token des registrierten <see cref="IAccessTokenProvider"/> als Bearer-Header (ohne Provider: kein Header).</summary>
public sealed class BearerTokenHandler(IServiceProvider services) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var provider = services.GetService<IAccessTokenProvider>();
        var token = provider is null ? null : await provider.GetAccessTokenAsync(cancellationToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
