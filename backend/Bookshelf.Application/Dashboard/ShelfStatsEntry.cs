using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Dashboard;

/// <summary>
/// One shelf entry, reduced to the four things the statistics ask of it: which pile it is in,
/// when it was finished, how long the book is, and what it is about.
/// </summary>
public record ShelfStatsEntry(
    Guid UserBookId,
    ReadingStatus Status,
    DateOnly? FinishedAt,
    int? PageCount,
    string[]? Subjects);
