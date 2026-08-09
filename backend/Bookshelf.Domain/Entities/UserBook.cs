using Bookshelf.Domain.Enums;

namespace Bookshelf.Domain.Entities;

public class UserBook
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BookId { get; set; }
    public ReadingStatus Status { get; set; }
    public int? Rating { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// Where this reader began: 0 for a book started from the front, 200 for one added while
    /// already half read. It exists because <see cref="CurrentPage"/> is recomputed as
    /// <c>StartPage + SUM(PagesRead)</c>, and a sum of increments has no way of knowing about
    /// pages read before the book was ever entered here.
    /// </summary>
    public int StartPage { get; set; }

    public int? CurrentPage { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? FinishedAt { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public Book Book { get; set; } = null!;
}
