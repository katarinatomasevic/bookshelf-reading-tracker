namespace Bookshelf.Application.ReadingLogs;

/// <summary>
/// A correction to one entry: the page count, and nothing else.
/// <para>
/// The date is not editable on purpose. Moving an entry to another day could land on a day that
/// already has one, and the unique constraint on <c>(UserBookId, Date)</c> would then force a
/// decision about merging that the reader never asked to make. A wrong day is fixed by deleting
/// the entry and adding it again — two obvious steps instead of one ambiguous one.
/// </para>
/// </summary>
public record UpdateReadingLogRequest(int PagesRead);
