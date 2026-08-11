using Bookshelf.Application.Books;
using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public class BookRepository(AppDbContext context) : IBookRepository
{
    public Task<Book?> GetByOpenLibraryIdAsync(string openLibraryId, CancellationToken cancellationToken) =>
        context.Books.FirstOrDefaultAsync(b => b.OpenLibraryId == openLibraryId, cancellationToken);

    public Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Books.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task AddAsync(Book book, CancellationToken cancellationToken)
    {
        context.Books.Add(book);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A single UPDATE that touches one column, without loading or tracking the entity — this
    /// runs while a page is being rendered, so it has no business pulling a row into the change
    /// tracker.
    ///
    /// The "still null" condition is not redundant with the caller's check. It makes the write
    /// idempotent and safe under concurrency: two readers opening the same corpus book at once
    /// both fetch the description, and the second one's UPDATE then affects no rows instead of
    /// overwriting the first. It also guarantees a description a reader typed by hand can never
    /// be replaced by Open Library's.
    /// </summary>
    public Task FillMissingDescriptionAsync(Guid bookId, string description, CancellationToken cancellationToken) =>
        context.Books
            .Where(b => b.Id == bookId && b.Description == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.Description, description),
                cancellationToken);
}
