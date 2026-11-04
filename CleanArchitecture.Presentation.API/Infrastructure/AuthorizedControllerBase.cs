using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CleanArchitecture.Presentation.API.Infrastructure;

/// <summary>Basis für Controller mit angemeldetem Benutzer. Die UserId kommt immer aus dem Token, nie aus dem Request.</summary>
public abstract class AuthorizedControllerBase : ControllerBase
{
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Angemeldeter Benutzer ohne NameIdentifier-Claim.");
}
