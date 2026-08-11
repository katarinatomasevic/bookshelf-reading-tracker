using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Book> Books => Set<Book>();
    public DbSet<UserBook> UserBooks => Set<UserBook>();
    public DbSet<ReadingLog> ReadingLogs => Set<ReadingLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Declared here rather than run by hand, so that Database.Migrate() emits
        // CREATE EXTENSION IF NOT EXISTS vector before the columns and index that need it.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
