using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Auth;

public interface IJwtTokenGenerator
{
    TokenResult GenerateToken(User user);
}
