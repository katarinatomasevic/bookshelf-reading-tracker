namespace Bookshelf.Application.Books;

public record BookDetailsDto(
    string OpenLibraryId,
    string Title,
    string? Author,
    string? Description,
    int? CoverId,
    string[]? Subjects,
    double? AverageRating,
    int? RatingsCount);
