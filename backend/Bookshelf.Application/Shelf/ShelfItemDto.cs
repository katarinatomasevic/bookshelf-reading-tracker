using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Shelf;

/// <summary>
/// One book on a user's shelf. Carries the book's own metadata as well, so the shelf page
/// renders from a single request; Note and Subjects are included because the shelf modal and
/// the client-side shelf search (next phase) read them without another round trip.
/// </summary>
public record ShelfItemDto(
    Guid Id,
    Guid BookId,
    string? OpenLibraryId,
    string Title,
    string? Author,
    int? CoverId,
    int? PageCount,
    string[]? Subjects,
    ReadingStatus Status,
    int? Rating,
    string? Note,
    int? CurrentPage,
    DateOnly? StartedAt,
    DateOnly? FinishedAt,
    DateTimeOffset AddedAt);
