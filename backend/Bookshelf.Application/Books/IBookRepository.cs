using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Books;

public interface IBookRepository
{
    Task<Book?> GetByOpenLibraryIdAsync(string openLibraryId, CancellationToken cancellationToken);

    Task<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(Book book, CancellationToken cancellationToken);
}
