namespace Bookshelf.Application.ReadingLogs;

public interface IReadingLogService
{
    Task<LogProgressResponse> LogProgressAsync(
        Guid userId, LogProgressRequest request, CancellationToken cancellationToken);
}
