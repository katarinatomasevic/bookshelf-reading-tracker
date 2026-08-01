using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookshelf.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Book");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired();

        // Manually added books have no Open Library key, and several of them may exist at
        // once; a plain unique index would collapse them all into a single NULL conflict.
        builder.HasIndex(b => b.OpenLibraryId)
            .IsUnique()
            .HasFilter("\"OpenLibraryId\" IS NOT NULL");

        builder.Property(b => b.CreatedAt)
            .IsRequired();
    }
}
