using Bookshelf.Application.Auth;
using Bookshelf.Application.Books;
using Bookshelf.Application.Dashboard;
using Bookshelf.Application.ReadingLogs;
using Bookshelf.Application.Recommendations;
using Bookshelf.Application.Shelf;
using Bookshelf.Infrastructure.Auth;
using Bookshelf.Infrastructure.ExternalServices.Embedding;
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

        // UseVector() registers the pgvector type mapping with Npgsql; without it EF cannot read
        // or write Book.Embedding at all.
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IShelfRepository, ShelfRepository>();
        services.AddScoped<IReadingLogRepository, ReadingLogRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
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

        var embeddingBaseUrl = configuration["Embedding:BaseUrl"]
            ?? throw new InvalidOperationException("Embedding:BaseUrl is not configured.");

        services.AddHttpClient<IEmbeddingClient, EmbeddingClient>(client =>
        {
            client.BaseAddress = new Uri(embeddingBaseUrl.TrimEnd('/') + "/");

            // Shorter than the Open Library client's ten seconds, and for the opposite reason.
            // This call sits inside "add a book to my shelf": the reader is waiting on it, and
            // the vector is optional. Encoding one short text takes milliseconds once the model
            // is loaded, so anything approaching five seconds means the service is not there —
            // at which point the right answer is to give up quickly and save the book without a
            // vector, not to make the reader wait for a nicety.
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
