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
}
