using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Shelf;

internal static class ShelfMappingExtensions
{
    public static ShelfItemDto ToShelfItemDto(this UserBook userBook) => new(
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
        userBook.CurrentPage,
        userBook.StartedAt,
        userBook.FinishedAt,
        userBook.AddedAt);
}
