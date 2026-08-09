using Bookshelf.Application.Shelf;

namespace Bookshelf.Application.ReadingLogs;

public interface IReadingLogService
{
    Task<LogProgressResponse> LogProgressAsync(
        Guid userId, LogProgressRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReadingLogDto>> GetHistoryAsync(
        Guid userId, Guid userBookId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the updated shelf entry rather than the edited log, the same way logging progress
    /// does: correcting an entry moves the reader's position, and the caller's shelf has to
    /// follow. The history list itself already knows what it just changed.
    /// </summary>
    Task<ShelfItemDto> UpdateAsync(
        Guid userId, Guid logId, UpdateReadingLogRequest request, CancellationToken cancellationToken);

    Task<ShelfItemDto> DeleteAsync(Guid userId, Guid logId, CancellationToken cancellationToken);
}
