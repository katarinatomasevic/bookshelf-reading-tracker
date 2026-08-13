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
using Npgsql;

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
            options.UseNpgsql(WithIterativeIndexScan(connectionString), npgsql => npgsql.UseVector()));

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

    /// <summary>
    /// Turns on pgvector's iterative index scan for every connection, and it is not optional.
    ///
    /// <para>
    /// HNSW is an approximate index: it walks its graph, collects roughly
    /// <c>hnsw.ef_search</c> (40 by default) nearest rows, and hands those to Postgres. Any
    /// <c>WHERE</c> clause is then applied to <em>that</em> set. When the filter is uncorrelated
    /// with distance this costs nothing, which is why it went unnoticed — but the recommender
    /// asks a question where the filter and the distance are almost perfectly correlated:
    /// "nearest to this Stephen King novel, but not by Stephen King". All forty rows the index
    /// returns are Stephen King, the filter removes all forty, and the query answers
    /// <b>zero rows</b> while thousands of matching books sit in the table.
    /// </para>
    ///
    /// <para>
    /// This was not a hypothetical. It was found by watching a reader with two King novels get
    /// two recommendations instead of ten, and reproduced down to a single SQL statement that
    /// returns 0 rows by default and 20 with this setting on.
    /// </para>
    ///
    /// <para>
    /// <c>strict_order</c> rather than <c>relaxed_order</c>: the index keeps scanning until it
    /// has enough rows that really do satisfy the filter, and returns them in true distance
    /// order. The safety net is <c>hnsw.max_scan_tuples</c>, 20000 by default, which is more than
    /// this corpus holds — so the worst case degrades to an exact scan of every book, measured at
    /// about 6 ms. That is the same trade the decisions document already states plainly: over
    /// ~15K rows the index is here because pgvector is part of the coursework, not because the
    /// data needs it.
    /// </para>
    ///
    /// <para>
    /// Set through the connection string rather than with a <c>SET</c> statement per query,
    /// because Npgsql pools connections: a session setting has to be applied when the connection
    /// is opened, or it applies to whichever queries happen to land on that pooled connection.
    /// </para>
    /// </summary>
    private static string WithIterativeIndexScan(string connectionString)
    {
        const string setting = "-c hnsw.iterative_scan=strict_order";

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        builder.Options = string.IsNullOrWhiteSpace(builder.Options)
            ? setting
            : $"{builder.Options} {setting}";

        return builder.ConnectionString;
    }
}
