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
}
