using Bookshelf.Application.Shelf;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.Repositories;

public class ShelfRepository(AppDbContext context) : IShelfRepository
{
    public Task<UserBook?> GetAsync(Guid userId, Guid bookId, CancellationToken cancellationToken) =>
        context.UserBooks
            .Include(ub => ub.Book)
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BookId == bookId, cancellationToken);

    public async Task AddAsync(UserBook userBook, CancellationToken cancellationToken)
    {
        context.UserBooks.Add(userBook);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserBook>> GetShelfAsync(
        Guid userId, ReadingStatus? status, ShelfSort sort, CancellationToken cancellationToken)
    {
        // Every shelf query is scoped by user id, never by row id alone.
        var query = context.UserBooks
            .Include(ub => ub.Book)
            .Where(ub => ub.UserId == userId);

        if (status is { } value)
        {
            query = query.Where(ub => ub.Status == value);
        }

        // Sorting by a nullable column puts unrated and unfinished books last rather than
        // first, which is what "highest rated" and "recently finished" are expected to mean.
        query = sort switch
        {
            ShelfSort.Title => query.OrderBy(ub => ub.Book.Title),
            ShelfSort.RatingDesc => query
                .OrderByDescending(ub => ub.Rating.HasValue)
                .ThenByDescending(ub => ub.Rating)
                .ThenByDescending(ub => ub.AddedAt),
            ShelfSort.FinishedDesc => query
                .OrderByDescending(ub => ub.FinishedAt.HasValue)
                .ThenByDescending(ub => ub.FinishedAt)
                .ThenByDescending(ub => ub.AddedAt),
            _ => query.OrderByDescending(ub => ub.AddedAt),
        };

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetOpenLibraryIdsOnShelfAsync(
        Guid userId, IReadOnlyCollection<string> openLibraryIds, CancellationToken cancellationToken)
    {
        if (openLibraryIds.Count == 0)
        {
            return new HashSet<string>();
        }

        var found = await context.UserBooks
            .Where(ub => ub.UserId == userId
                && ub.Book.OpenLibraryId != null
                && openLibraryIds.Contains(ub.Book.OpenLibraryId!))
            .Select(ub => ub.Book.OpenLibraryId!)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }
}
