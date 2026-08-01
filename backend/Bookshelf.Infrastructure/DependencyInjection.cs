using Bookshelf.Application.Auth;
using Bookshelf.Application.Books;
using Bookshelf.Infrastructure.Auth;
using Bookshelf.Infrastructure.ExternalServices.OpenLibrary;
using Bookshelf.Infrastructure.Persistence;
using Bookshelf.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        var openLibraryBaseUrl = configuration["OpenLibrary:BaseUrl"]
            ?? throw new InvalidOperationException("OpenLibrary:BaseUrl is not configured.");
        var openLibraryUserAgent = configuration["OpenLibrary:UserAgent"]
            ?? throw new InvalidOperationException("OpenLibrary:UserAgent is not configured.");

        services.AddHttpClient<IOpenLibraryClient, OpenLibraryClient>(client =>
        {
            client.BaseAddress = new Uri(openLibraryBaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(openLibraryUserAgent);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
