using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Books;

public interface IBookRepository
{
    Task<Book?> GetByOpenLibraryIdAsync(string openLibraryId, CancellationToken cancellationToken);

    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Book book, CancellationToken cancellationToken);

    /// <summary>
    /// Fills in a description that was missing, and only if it is still missing.
    ///
    /// Exists because of the seed corpus. Open Library's search API returns no descriptions, so
    /// the ~15.5K harvested books are stored without one — while the rule for reading a book is
    /// "if it is in our database, serve it from there without calling Open Library". That rule
    /// was written when being in the database implied someone had added the book to a shelf,
    /// which fetched its full details. The corpus broke that implication, and this repairs it.
    /// </summary>
    Task FillMissingDescriptionAsync(Guid bookId, string description, CancellationToken cancellationToken);
}
