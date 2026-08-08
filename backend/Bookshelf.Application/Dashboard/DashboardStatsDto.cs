namespace Bookshelf.Application.Dashboard;

/// <summary>
/// The three cards. Note that <paramref name="TotalPages"/> and <paramref name="AveragePagesPerDay"/>
/// are deliberately not the same number divided by something: the first measures the library
/// (a finished book counts in full, logged or not), the second measures the rhythm of logging
/// (only what was actually written down). Dividing the first by days would let a book finished
/// years ago and entered in one go claim hundreds of pages a day.
/// </summary>
public record DashboardStatsDto(int BooksRead, int TotalPages, int AveragePagesPerDay);
