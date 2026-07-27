namespace Bookshelf.Application.Auth;

public record AuthResponse(Guid UserId, string Email, string DisplayName, string Token, DateTimeOffset ExpiresAt);
