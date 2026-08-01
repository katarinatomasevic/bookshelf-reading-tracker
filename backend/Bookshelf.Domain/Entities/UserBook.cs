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
    public int? CurrentPage { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? FinishedAt { get; set; }
    public DateTimeOffset AddedAt { get; set; }

    public Book Book { get; set; } = null!;
}
