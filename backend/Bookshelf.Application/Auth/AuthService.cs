using Bookshelf.Application.Common;
using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Auth;

public class AuthService(IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await userRepository.GetByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            throw new ValidationException("Email is already registered.");
        }

        PasswordPolicy.Validate(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await userRepository.AddAsync(user);

        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await userRepository.GetByEmailAsync(request.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("Invalid email or password.");
        }

        return BuildAuthResponse(user);
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var token = jwtTokenGenerator.GenerateToken(user);
        return new AuthResponse(user.Id, user.Email, user.DisplayName, token.Token, token.ExpiresAt);
    }
}
