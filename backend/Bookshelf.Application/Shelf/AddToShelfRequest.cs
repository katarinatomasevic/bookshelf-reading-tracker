namespace Bookshelf.Application.Shelf;

/// <summary>
/// Exactly one of <see cref="OpenLibraryId"/> and <see cref="BookId"/> identifies the book.
/// PageCount and Isbn are optional hints from the search results: the Open Library work
/// endpoint returns neither, so without them a book added from search would lose its page
/// count — which later phases need for reading progress.
/// </summary>
public record AddToShelfRequest(
    string? OpenLibraryId,
    Guid? BookId,
    int? PageCount,
    string? Isbn);
