using Bookshelf.Application.Books;
using Bookshelf.Domain.Enums;

namespace Bookshelf.Application.Shelf;

public interface IShelfService
{
    Task<ShelfItemDto> AddAsync(Guid userId, AddToShelfRequest request, CancellationToken cancellationToken);

    /// <summary>Creates a book Open Library does not have, together with its shelf entry.</summary>
    Task<ShelfItemDto> AddManualAsync(Guid userId, ManualBookRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShelfItemDto>> GetShelfAsync(
        Guid userId, ReadingStatus? status, string? sort, CancellationToken cancellationToken);
}
