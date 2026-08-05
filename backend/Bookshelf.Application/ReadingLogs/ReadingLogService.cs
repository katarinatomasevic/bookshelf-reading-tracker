using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Application.Shelf;
using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.ReadingLogs;

public class ReadingLogService(
    IShelfRepository shelfRepository,
    IReadingLogRepository readingLogRepository) : IReadingLogService
{
    /// <summary>
    /// How far back progress may be entered. Far enough to cover a forgotten week, short enough
    /// that a mistyped year cannot quietly rewrite last spring's statistics.
    /// </summary>
    private const int MaxBackdatedDays = 30;

    /// <summary>
    /// Time zones run ahead of UTC, so a reader's own calendar day can legitimately be one day
    /// ahead of the server's. The same allowance the shelf PATCH makes.
    /// </summary>
    private const int FutureToleranceDays = 1;

    /// <summary>
    /// Records a day's reading and moves the shelf entry to match, in one transaction. The two
    /// belong together: a log written without its CurrentPage — or the reverse — would leave the
    /// progress bar disagreeing with the history behind it.
    /// </summary>
    public async Task<LogProgressResponse> LogProgressAsync(
        Guid userId, LogProgressRequest request, CancellationToken cancellationToken)
    {
        // Scoped by user id, never by the row id alone: someone else's guid must find nothing
        // rather than find a book and then be refused.
        var userBook = await shelfRepository.GetByIdAsync(userId, request.UserBookId, cancellationToken)
            ?? throw new NotFoundException("Book not found on your shelf.");

        var date = ValidateDate(request.Date);

        // Null means the book was never opened; page 0 is where that reader stands.
        var currentPage = userBook.CurrentPage ?? 0;
        var pageCount = userBook.Book.PageCount;

        var pagesRead = ResolvePagesRead(request, currentPage);
        var newPage = currentPage + pagesRead;

        // Only checkable when Open Library gave us a page count; without one there is no end to
        // run past, and refusing on a guess would block a legitimate entry.
        if (pageCount is { } total && newPage > total)
        {
            throw new ValidationException(
                $"That would take you past page {total}, the last page of this book.");
        }

        var existing = await readingLogRepository.GetByDateAsync(userBook.Id, date, cancellationToken);
        if (existing is not null)
        {
            // Sitting down with the same book twice in a day is one day of reading, not two:
            // a separate row would make the activity chart and the streak count that day twice.
            existing.PagesRead += pagesRead;
        }
        else
        {
            readingLogRepository.Add(new ReadingLog
            {
                Id = Guid.NewGuid(),
                UserBookId = userBook.Id,
                Date = date,
                PagesRead = pagesRead,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        userBook.CurrentPage = newPage;

        // Logging progress is proof the book was started, and the day it was logged for is the
        // best evidence of when — the entry may well be a backdated one.
        userBook.StartedAt ??= date;

        await readingLogRepository.SaveProgressAsync(cancellationToken);

        return new LogProgressResponse(
            userBook.ToShelfItemDto(),
            pageCount is { } lastPage && newPage >= lastPage);
    }

    /// <summary>
    /// Turns whichever number the reader entered into the increment the log stores. A position
    /// is only meaningful as "further than where I was", so it is checked against the current
    /// page; an increment stands on its own.
    /// </summary>
    private static int ResolvePagesRead(LogProgressRequest request, int currentPage)
    {
        var hasPagesRead = request.PagesRead is not null;
        var hasToPage = request.ToPage is not null;

        if (hasPagesRead == hasToPage)
        {
            throw new ValidationException("Send either pagesRead or toPage, not both.");
        }

        if (request.PagesRead is { } pagesRead)
        {
            if (pagesRead <= 0)
            {
                throw new ValidationException("Pages read must be greater than 0.");
            }

            return pagesRead;
        }

        var toPage = request.ToPage!.Value;
        if (toPage <= currentPage)
        {
            throw new ValidationException(
                $"You are already on page {currentPage}. Enter a page further along.");
        }

        return toPage - currentPage;
    }

    /// <summary>
    /// The date is the reader's, but not without limits: a future day would let the streak be
    /// filled in advance, and anything older than the window is far likelier to be a typo than
    /// a memory.
    /// </summary>
    private static DateOnly ValidateDate(DateOnly date)
    {
        var serverToday = DateOnly.FromDateTime(DateTime.UtcNow);

        if (date > serverToday.AddDays(FutureToleranceDays))
        {
            throw new ValidationException("The date cannot be in the future.");
        }

        if (date < serverToday.AddDays(-MaxBackdatedDays))
        {
            throw new ValidationException(
                $"Progress can only be logged for the last {MaxBackdatedDays} days.");
        }

        return date;
    }
}
