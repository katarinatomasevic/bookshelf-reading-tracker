using Pgvector;

namespace Bookshelf.Domain.Entities;

public class Book
{
    public Guid Id { get; set; }

    /// <summary>Open Library work key (e.g. OL893415W). Null for manually added books.</summary>
    public string? OpenLibraryId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }
    public int? CoverId { get; set; }
    public int? PageCount { get; set; }
    public string? Isbn { get; set; }
    public string[]? Subjects { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// 384-dimension sentence embedding of "title. author. subjects.", produced by the Python
    /// embedding service (all-MiniLM-L6-v2) and stored as a pgvector column. Null while the book
    /// has not been embedded yet — the recommender treats that as "not a candidate" rather than
    /// as an error, because a failing embedding service must never block adding a book.
    ///
    /// This is the one place where the domain model touches a third-party type (Pgvector.Vector).
    /// The alternative — an EF shadow property — would keep the entity clean but push every
    /// nearest-neighbour query into EF.Property&lt;&gt; gymnastics, so the trade was made knowingly.
    /// </summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// Position of this book inside its <see cref="SeedTopic"/> during the corpus harvest, 1-based
    /// and following Open Library's own readinglog ordering. Null for books added by readers —
    /// which doubles as the marker that separates the seed corpus from user-created rows.
    /// </summary>
    public int? PopularityRank { get; set; }

    /// <summary>
    /// The harvest topic this book was first seen under (a book listed under several topics keeps
    /// the first one, so the harvest stays deterministic). Null for books added by readers.
    /// Cold-start recommendations group by this column because <see cref="Subjects"/> is far too
    /// noisy to group on.
    /// </summary>
    public string? SeedTopic { get; set; }
}
