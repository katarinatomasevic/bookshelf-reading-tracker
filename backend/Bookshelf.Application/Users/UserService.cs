using Bookshelf.Application.Auth;
using Bookshelf.Application.Common;
using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Users;

public class UserService(IUserRepository userRepository) : IUserService
{
    public async Task<UserDto> GetProfileAsync(Guid userId)
    {
        var user = await GetUserOrThrowAsync(userId);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateUserRequest request)
    {
        var user = await GetUserOrThrowAsync(userId);

        user.DisplayName = request.DisplayName;

        if (!string.IsNullOrEmpty(request.NewPassword))
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) ||
                !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new ValidationException("Current password is incorrect.");
            }

            PasswordPolicy.Validate(request.NewPassword);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        await userRepository.UpdateAsync(user);

        return ToDto(user);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId) =>
        await userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

    private static UserDto ToDto(User user) => new(user.Id, user.Email, user.DisplayName, user.CreatedAt);
}
