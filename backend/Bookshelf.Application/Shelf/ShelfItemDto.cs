using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Shelf;

/// <summary>
/// One book on a user's shelf. Carries the book's own metadata as well, so the shelf page
/// renders from a single request; Note and Subjects are included because the shelf modal and
/// the client-side shelf search read them without another round trip.
/// <para>
/// <see cref="LogCount"/> is the number of reading log entries behind this book. It travels with
/// the shelf so the modal can decide whether to offer a reading history at all, and name the
/// number of entries in the heading, without fetching the entries themselves — they are loaded
/// only if the reader actually opens the section.
/// </para>
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
    int StartPage,
    int? CurrentPage,
    DateOnly? StartedAt,
    DateOnly? FinishedAt,
    DateTimeOffset AddedAt,
    int LogCount);
