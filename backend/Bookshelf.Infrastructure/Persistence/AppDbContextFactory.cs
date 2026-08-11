using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Bookshelf.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` create migrations without booting the full Api host (which would
/// otherwise run Program.cs's Database.Migrate() as a side effect and require a live DB).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5433;Database=bookshelf;Username=bookshelf;Password=bookshelf";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        // Must mirror DependencyInjection.AddInfrastructure: the design-time context needs the
        // same pgvector mapping, otherwise `dotnet ef` cannot build a model containing Vector.
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.UseVector());

        return new AppDbContext(optionsBuilder.Options);
    }
}
