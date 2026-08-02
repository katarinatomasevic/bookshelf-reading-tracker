using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Shelf;

public interface IShelfRepository
{
    Task<UserBook?> GetAsync(Guid userId, Guid bookId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads one shelf entry by its own id, scoped to its owner. The user id is part of the
    /// lookup rather than a check afterwards, so another user's guid simply finds nothing.
    /// </summary>
    Task<UserBook?> GetByIdAsync(Guid userId, Guid userBookId, CancellationToken cancellationToken);

    Task AddAsync(UserBook userBook, CancellationToken cancellationToken);

    Task UpdateAsync(UserBook userBook, CancellationToken cancellationToken);

    Task DeleteAsync(UserBook userBook, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<ReadingStatus, int>> GetCountsAsync(
        Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserBook>> GetShelfAsync(
        Guid userId, ReadingStatus? status, ShelfSort sort, CancellationToken cancellationToken);

    /// <summary>Of the given Open Library keys, returns those the user already has on their shelf.</summary>
    Task<IReadOnlySet<string>> GetOpenLibraryIdsOnShelfAsync(
        Guid userId, IReadOnlyCollection<string> openLibraryIds, CancellationToken cancellationToken);
}
