using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Shelf;

internal static class ShelfMappingExtensions
{
    /// <summary>
    /// The log count is passed in rather than read off the entity. UserBook has no collection of
    /// logs on purpose: giving it one would tempt every shelf query into loading every reading
    /// log a user has ever written, only to count them. The callers each have the cheapest source
    /// at hand — a grouped count for the whole shelf, the loaded rows when a position has just
    /// been recomputed, a single count otherwise.
    /// </summary>
    public static ShelfItemDto ToShelfItemDto(this UserBook userBook, int logCount) => new(
        userBook.Id,
        userBook.BookId,
        userBook.Book.OpenLibraryId,
        userBook.Book.Title,
        userBook.Book.Author,
        userBook.Book.CoverId,
        userBook.Book.PageCount,
        userBook.Book.Subjects,
        userBook.Status,
        userBook.Rating,
        userBook.Note,
        userBook.StartPage,
        userBook.CurrentPage,
        userBook.StartedAt,
        userBook.FinishedAt,
        userBook.AddedAt,
        logCount);
}
