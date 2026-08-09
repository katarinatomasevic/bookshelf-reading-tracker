using Bookshelf.Application.Common.Exceptions;
using Bookshelf.Application.Shelf;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;

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
        var userBook = await GetShelfEntryAsync(userId, request.UserBookId, cancellationToken);

        // A book waiting to be read has no position — the F3 transition rule clears it, and the
        // recomputation below deliberately leaves such an entry alone. Accepting the log anyway
        // would write a row that moves nothing and shows up nowhere, so the reader is told what
        // to do instead. Phase 5 allowed this; back then the position was raised incrementally
        // regardless of status, so nothing could go quietly out of step. See the phase 7 report.
        if (userBook.Status == ReadingStatus.WantToRead)
        {
            throw new ValidationException(
                "Set the book to Reading first, then log your progress.");
        }

        var date = ValidateDate(request.Date);

        // Null means the book was never opened; page 0 is where that reader stands.
        var currentPage = userBook.CurrentPage ?? 0;

        var pagesRead = ResolvePagesRead(request, currentPage);

        // The whole history, because the position is recomputed from the full sum rather than
        // nudged by the increment — see ReadingPosition. It also serves as the lookup for the
        // day's existing entry, so this replaces what used to be a separate query.
        var logs = await readingLogRepository.GetForUserBookAsync(userBook.Id, cancellationToken);

        var entry = logs.FirstOrDefault(log => log.Date == date);
        if (entry is not null)
        {
            // Sitting down with the same book twice in a day is one day of reading, not two:
            // a separate row would make the activity chart and the streak count that day twice.
            entry.PagesRead += pagesRead;
        }
        else
        {
            entry = new ReadingLog
            {
                Id = Guid.NewGuid(),
                UserBookId = userBook.Id,
                Date = date,
                PagesRead = pagesRead,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            readingLogRepository.Add(entry);
            logs = [.. logs, entry];
        }

        var loggedPages = SumPages(logs);

        ReadingPosition.EnsureWithinBook(userBook, loggedPages);
        ReadingPosition.Apply(userBook, loggedPages);

        // Logging progress is proof the book was started, and the day it was logged for is the
        // best evidence of when — the entry may well be a backdated one.
        userBook.StartedAt ??= date;

        await readingLogRepository.SaveProgressAsync(cancellationToken);

        return new LogProgressResponse(
            userBook.ToShelfItemDto(logs.Count),
            userBook.Book.PageCount is { } lastPage && userBook.CurrentPage >= lastPage,
            entry.PagesRead);
    }

    /// <summary>
    /// The history behind one book, newest first. Fetched only when the reader opens the section
    /// in the shelf modal, which is why the shelf listing carries a count and not the entries.
    /// </summary>
    public async Task<IReadOnlyList<ReadingLogDto>> GetHistoryAsync(
        Guid userId, Guid userBookId, CancellationToken cancellationToken)
    {
        // Resolved through the shelf first, so a book that is not this user's returns 404 rather
        // than an empty list that would look like a book with no reading behind it.
        var userBook = await GetShelfEntryAsync(userId, userBookId, cancellationToken);

        var logs = await readingLogRepository.GetForUserBookAsync(userBook.Id, cancellationToken);

        return logs
            .Select(log => new ReadingLogDto(log.Id, log.Date, log.PagesRead))
            .ToArray();
    }

    /// <summary>
    /// Corrects the pages recorded for one day. Nothing cascades: because the log stores
    /// increments rather than positions, changing one day does not alter what any other day
    /// meant — only the total moves, and the position is recomputed from that total.
    /// </summary>
    public async Task<ShelfItemDto> UpdateAsync(
        Guid userId, Guid logId, UpdateReadingLogRequest request, CancellationToken cancellationToken)
    {
        if (request.PagesRead <= 0)
        {
            // Zero is not a correction, it is a deletion, and there is an endpoint for that.
            // The database says the same through CK_ReadingLog_PagesRead.
            throw new ValidationException(
                "Pages read must be greater than 0. To remove the entry, delete it instead.");
        }

        var (log, userBook, logs) = await LoadForWriteAsync(userId, logId, cancellationToken);

        log.PagesRead = request.PagesRead;

        await RecalculateAndSaveAsync(userBook, logs, cancellationToken);

        return userBook.ToShelfItemDto(logs.Count);
    }

    /// <summary>
    /// Removes one day's entry. No page check is needed on the way out: a smaller sum can only
    /// move the reader backwards, never past the end of the book.
    /// </summary>
    public async Task<ShelfItemDto> DeleteAsync(
        Guid userId, Guid logId, CancellationToken cancellationToken)
    {
        var (log, userBook, logs) = await LoadForWriteAsync(userId, logId, cancellationToken);

        readingLogRepository.Remove(log);

        var remaining = logs.Where(entry => entry.Id != log.Id).ToArray();

        await RecalculateAndSaveAsync(userBook, remaining, cancellationToken);

        return userBook.ToShelfItemDto(remaining.Length);
    }

    /// <summary>
    /// Loads an entry, the shelf entry it belongs to, and that book's whole history. Both
    /// lookups are scoped by user id, so a log id belonging to someone else is a 404 and never
    /// a 403 that would confirm the row exists.
    /// </summary>
    private async Task<(ReadingLog Log, UserBook UserBook, IReadOnlyList<ReadingLog> Logs)>
        LoadForWriteAsync(Guid userId, Guid logId, CancellationToken cancellationToken)
    {
        var log = await readingLogRepository.GetByIdAsync(userId, logId, cancellationToken)
            ?? throw new NotFoundException("Reading log entry not found.");

        var userBook = await GetShelfEntryAsync(userId, log.UserBookId, cancellationToken);

        var logs = await readingLogRepository.GetForUserBookAsync(userBook.Id, cancellationToken);

        return (log, userBook, logs);
    }

    /// <summary>
    /// The shared tail of every correction: total up what is left, check it still fits inside the
    /// book, write the position, save. Validation happens before the position is written so a
    /// rejected edit leaves the entry exactly as it was.
    /// <para>
    /// The entries are summed in memory rather than by the database, because an edit or a removal
    /// staged on the tracked entity has not been written yet — a <c>SUM</c> query at this point
    /// would still be reporting the old numbers.
    /// </para>
    /// </summary>
    private async Task RecalculateAndSaveAsync(
        UserBook userBook, IReadOnlyList<ReadingLog> logs, CancellationToken cancellationToken)
    {
        var loggedPages = SumPages(logs);

        ReadingPosition.EnsureWithinBook(userBook, loggedPages);
        ReadingPosition.Apply(userBook, loggedPages);

        await readingLogRepository.SaveProgressAsync(cancellationToken);
    }

    /// <summary>
    /// Scoped by user id, never by the row id alone: someone else's guid must find nothing
    /// rather than find a book and then be refused.
    /// </summary>
    private async Task<UserBook> GetShelfEntryAsync(
        Guid userId, Guid userBookId, CancellationToken cancellationToken) =>
        await shelfRepository.GetByIdAsync(userId, userBookId, cancellationToken)
            ?? throw new NotFoundException("Book not found on your shelf.");

    private static int SumPages(IEnumerable<ReadingLog> logs) => logs.Sum(log => log.PagesRead);

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
    /// <para>
    /// This applies to writing a new entry only. Correcting or deleting an old one is not bound
    /// by the window: the date is not what is changing, and a mistake found late is still worth
    /// fixing.
    /// </para>
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
