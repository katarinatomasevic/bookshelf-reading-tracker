using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Shelf;

public interface IShelfRepository
{
    Task<UserBook?> GetAsync(Guid userId, Guid bookId, CancellationToken cancellationToken);

    Task AddAsync(UserBook userBook, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserBook>> GetShelfAsync(
        Guid userId, ReadingStatus? status, ShelfSort sort, CancellationToken cancellationToken);

    /// <summary>Of the given Open Library keys, returns those the user already has on their shelf.</summary>
    Task<IReadOnlySet<string>> GetOpenLibraryIdsOnShelfAsync(
        Guid userId, IReadOnlyCollection<string> openLibraryIds, CancellationToken cancellationToken);
}
