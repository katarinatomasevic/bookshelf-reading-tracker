using Bookshelf.Application.Shelf;

namespace Bookshelf.Application.ReadingLogs;

/// <summary>
/// The whole shelf entry as it now stands, not just the new page number: logging progress also
/// moves <c>CurrentPage</c> and can fill in <c>StartedAt</c>, so returning the same shape a
/// shelf PATCH returns lets the client refresh its state through one mechanism instead of two.
/// <para>
/// <see cref="BookCompleted"/> reports that the reader reached the last page; it deliberately
/// does not change the status. The book may have an afterword, the page count from Open Library
/// may be wrong, or the number may simply have been mistyped — so the client asks first.
/// It is always <c>false</c> when the page count is unknown, since there is no end to reach.
/// </para>
/// <para>
/// <see cref="DayTotal"/> is everything now recorded for that date, which is not always what was
/// just entered: a second sitting with the same book adds to the day already there rather than
/// starting a new one. Without this number the client could only report back what the reader
/// typed, and the one-entry-per-day rule would stay invisible until they opened the history and
/// wondered why three entries had become one row.
/// </para>
/// </summary>
public record LogProgressResponse(ShelfItemDto Item, bool BookCompleted, int DayTotal);
