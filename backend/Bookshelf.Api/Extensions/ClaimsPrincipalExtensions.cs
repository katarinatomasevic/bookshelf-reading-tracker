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
}
