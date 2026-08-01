namespace Bookshelf.Application.Books;

public record BookSearchResult(
    string OpenLibraryId,
    string Title,
    string? Author,
    int? FirstPublishYear,
    int? CoverId,
    int? PageCount,
    string[]? Subjects,
    string? Isbn)
{
    /// <summary>
    /// Set only when the caller sent a valid token: the search endpoint stays public, but a
    /// logged-in user sees which results are already on their shelf.
    /// </summary>
    public bool IsOnShelf { get; init; }
}
