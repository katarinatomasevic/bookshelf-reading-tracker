namespace Bookshelf.Application.Users;

public interface IUserService
{
    Task<UserDto> GetProfileAsync(Guid userId);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateUserRequest request);
}
