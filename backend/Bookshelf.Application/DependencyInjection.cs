using Bookshelf.Application.Auth;
using Bookshelf.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
