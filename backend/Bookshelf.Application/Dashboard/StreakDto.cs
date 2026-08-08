namespace Bookshelf.Application.Dashboard;

/// <param name="Current">Consecutive days with an entry, counted back from today or yesterday.</param>
/// <param name="Longest">The best such run in the whole history — the number worth beating.</param>
public record StreakDto(int Current, int Longest);
