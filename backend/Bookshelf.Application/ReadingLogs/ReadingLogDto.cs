namespace Bookshelf.Application.ReadingLogs;

/// <summary>
/// One entry in a book's reading history. Only what the history list shows and can act on: the
/// date it belongs to, the pages it records, and the id needed to correct or remove it.
/// <para>
/// <c>CreatedAt</c> is deliberately left out. It says when the row was written, which for a
/// backdated entry is a different day from the one being reported, and showing both would raise
/// a question the list has no reason to raise.
/// </para>
/// </summary>
public record ReadingLogDto(
    Guid Id,
    DateOnly Date,
    int PagesRead);
