namespace Bookshelf.Application.Books;

/// <summary>A book Open Library does not have; created together with its shelf entry.</summary>
public record ManualBookRequest(
    string Title,
    string? Author,
    string? Description,
    int? PageCount,
    string[]? Subjects);
