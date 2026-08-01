namespace Bookshelf.Application.Books;

public record BookSearchResult(
    string OpenLibraryId,
    string Title,
    string? Author,
    int? FirstPublishYear,
    int? CoverId,
    int? PageCount,
    string[]? Subjects,
    string? Isbn);
