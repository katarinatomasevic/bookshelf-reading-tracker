namespace Bookshelf.Application.Users;

public record UpdateUserRequest(string DisplayName, string? CurrentPassword, string? NewPassword);
