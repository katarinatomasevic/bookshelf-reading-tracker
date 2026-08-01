namespace Bookshelf.Application.Books;

public record BookSearchPageResult(
    IReadOnlyList<BookSearchResult> Items,
    int Page,
    bool HasMore);
