namespace Bookshelf.Application.Dashboard;

/// <summary>
/// The activity grid: 26 whole weeks of daily totals, every day present even when it is a zero,
/// so the client can lay out 7 x 26 cells without working out which days are missing.
/// </summary>
/// <param name="From">The Monday the grid starts on.</param>
/// <param name="To">The Sunday it ends on — the current week is shown whole, so this can be a
/// day or two ahead of today. Those cells are simply empty.</param>
public record ActivityDto(DateOnly From, DateOnly To, IReadOnlyList<ActivityDayDto> Days);

public record ActivityDayDto(DateOnly Date, int Pages);
