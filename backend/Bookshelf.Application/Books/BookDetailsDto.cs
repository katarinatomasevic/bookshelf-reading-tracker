namespace Bookshelf.Application.Books;

public record BookDetailsDto(
    string? OpenLibraryId,
    string Title,
    string? Author,
    string? Description,
    int? CoverId,
    string[]? Subjects,
    double? AverageRating,
    int? RatingsCount)
{
    /// <summary>Set only for books stored in our database; null while a book exists on Open Library only.</summary>
    public Guid? Id { get; init; }

    public int? PageCount { get; init; }

    public string? Isbn { get; init; }

    /// <summary>Set only when the caller sent a valid token (see <see cref="BookSearchResult.IsOnShelf"/>).</summary>
    public bool IsOnShelf { get; init; }
}
