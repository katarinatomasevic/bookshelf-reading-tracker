namespace Bookshelf.Application.Books;

public record OpenLibraryWorkData(
    string Title,
    string? Description,
    IReadOnlyList<string> AuthorKeys,
    string[]? Subjects,
    int? CoverId);
