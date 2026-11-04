namespace CleanArchitecture.Contracts;

/// <summary>Liefert das Access-Token für Aufrufe der API. Wird vom jeweiligen Frontend implementiert und registriert.</summary>
public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}
