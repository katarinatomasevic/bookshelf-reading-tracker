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

        // 384 is not a free choice: it is the output width of all-MiniLM-L6-v2, the model the
        // Python embedding service loads. pgvector fixes the width in the column type, so the
        // two numbers have to be changed together if the model is ever swapped.
        builder.Property(b => b.Embedding)
            .HasColumnType("vector(384)");

        // HNSW rather than IVFFlat. IVFFlat computes its cluster centroids from the rows that
        // already exist, so it cannot be created on an empty table — it would have to be a manual
        // step run after the seed, outside migrations. HNSW builds its graph incrementally, works
        // on an empty table, and lets new books insert themselves into the structure, which keeps
        // the index inside this EF migration where the rest of the schema lives.
        //
        // Honest note for the defence: over ~10K rows an exact scan takes about ten milliseconds
        // and no index is actually needed. This exists because pgvector is part of the course, not
        // because performance demanded it.
        builder.HasIndex(b => b.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
