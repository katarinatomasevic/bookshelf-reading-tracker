using System.Security.Claims;

namespace Bookshelf.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("Token does not contain a user id claim.");

        return Guid.Parse(value);
    }

    /// <summary>
    /// For endpoints that are public but behave differently for a logged-in user. Authentication
    /// still validates the token; an absent or unusable one simply means "guest" instead of 401.
    /// </summary>
    public static Guid? GetUserIdOrNull(this ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
