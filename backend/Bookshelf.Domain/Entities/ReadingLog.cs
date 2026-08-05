namespace Bookshelf.Domain.Entities;

/// <summary>
/// One day's reading of one book. <see cref="PagesRead"/> is an increment ("30 pages today"),
/// never a position: the chart and the streak measure daily volume, and a stored position would
/// have to be read as the difference between neighbouring rows — which breaks the moment a day
/// is skipped or an entry is corrected.
/// </summary>
public class ReadingLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// The log hangs off the shelf entry rather than off the user and the book separately,
    /// because progress belongs to one person's reading of one book — which is what UserBook is.
    /// </summary>
    public Guid UserBookId { get; set; }

    public DateOnly Date { get; set; }
    public int PagesRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
