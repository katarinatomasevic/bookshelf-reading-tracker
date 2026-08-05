namespace Bookshelf.Application.ReadingLogs;

/// <summary>
/// A day's progress on one book. Exactly one of <see cref="PagesRead"/> and <see cref="ToPage"/>
/// is sent: the reader thinks either "I read 30 pages" or "I'm on page 150", and the service
/// translates whichever arrives into the increment the log actually stores.
/// <para>
/// <see cref="Date"/> comes from the client rather than the server clock. A server on UTC would
/// stamp yesterday on someone logging progress at 01:30 local time and break their streak for
/// no reason; the same field also lets a reader enter a day they forgot.
/// </para>
/// </summary>
public record LogProgressRequest(
    Guid UserBookId,
    DateOnly Date,
    int? PagesRead,
    int? ToPage);
