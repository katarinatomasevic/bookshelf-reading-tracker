namespace Bookshelf.Application.Users;

public record UserDto(Guid Id, string Email, string DisplayName, DateTimeOffset CreatedAt);
