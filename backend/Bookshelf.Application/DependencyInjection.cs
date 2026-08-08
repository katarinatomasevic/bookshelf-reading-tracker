using Bookshelf.Application.Auth;
using Bookshelf.Application.Books;
using Bookshelf.Application.Dashboard;
using Bookshelf.Application.ReadingLogs;
using Bookshelf.Application.Shelf;
using Bookshelf.Application.Users;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IShelfService, ShelfService>();
        services.AddScoped<IReadingLogService, ReadingLogService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddMemoryCache(options => options.SizeLimit = 1000);

        return services;
    }
}
