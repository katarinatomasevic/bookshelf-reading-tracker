namespace Bookshelf.Application.Auth;

public record TokenResult(string Token, DateTimeOffset ExpiresAt);
