using Bookshelf.Application.Common;
using Bookshelf.Domain.Entities;
using Bookshelf.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.DemoData;

/// <summary>
/// Creates the account used to demonstrate the application: a reader with a full shelf, a year of
/// finished books behind her and a live reading streak.
/// <para>
/// It writes to the database directly rather than driving the HTTP API, and that is forced rather
/// than chosen: <c>POST /api/reading-log</c> refuses any date more than thirty days old, so an API
/// seed could only ever fill the last month of a twenty-six week activity grid. Writing directly
/// means the invariants the endpoint would have enforced have to be met here instead, which is why
/// <see cref="DemoDataCheck"/> runs afterwards and prints what it found.
/// </para>
/// <para>
/// It runs inside the API host — see Program.cs — so the password is hashed by the same BCrypt
/// call that <c>AuthService</c> uses and the rows go through the same <see cref="AppDbContext"/>
/// and the same entity configuration as everything else. A separate seeding tool would have had to
/// restate both, and could drift from them.
/// </para>
/// </summary>
public static class DemoDataSeeder
{
    public const string Email = "katarina@bookshelf.com";
    public const string Password = "Citam2026!";
    public const string DisplayName = "Kaca";

    /// <summary>Days between a book reaching the shelf and the first page being read.</summary>
    private const int AddedBeforeStartDays = 2;

    /// <summary>How long a book with no log entries is taken to have been read for.</summary>
    private const int UnloggedReadingDays = 21;

    public static async Task<int> RunAsync(
        AppDbContext dbContext, bool refresh, CancellationToken cancellationToken = default)
    {
        // The reader's own calendar day, not the server's UTC day. Everything downstream hangs off
        // this: the dashboard anchors the streak on a `today` the browser sends from local date
        // parts, so a seed anchored on UtcNow would, in the hours after midnight in Belgrade,
        // stamp its last entry on what the browser still calls yesterday — leaving today's square
        // on the activity grid empty on the one screen the demo is built around.
        var today = DateOnly.FromDateTime(DateTime.Now);

        var existing = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == Email, cancellationToken);

        if (existing is not null)
        {
            if (!refresh)
            {
                Console.WriteLine(
                    $"The demo account {Email} already exists, so nothing was written.\n"
                    + "Re-run with --refresh to rebuild it against today's date — the reading\n"
                    + "history is generated relative to the day the seed runs, so an account\n"
                    + "seeded a week ago shows a broken streak.");
                return 0;
            }

            // Only ever this one account. UserBook and ReadingLog follow through their cascades;
            // the books themselves are shared corpus rows and are deliberately left alone.
            dbContext.Users.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
            Console.WriteLine($"Removed the previous {Email} and its shelf.");
        }

        // Validated rather than assumed: the constant above has to satisfy the same rule the
        // registration form enforces, or the demo account would be one the application itself
        // would have refused to create.
        PasswordPolicy.Validate(Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            DisplayName = DisplayName,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.Users.Add(user);

        Console.WriteLine("Resolving the demo shelf against the seeded corpus");
        var resolved = await DemoBookPicker.ResolveAsync(dbContext, DemoBooks.All, cancellationToken);

        foreach (var (slot, book) in resolved)
        {
            var matched = string.Equals(book.Title, slot.Title, StringComparison.OrdinalIgnoreCase);
            Console.WriteLine(matched
                ? $"  {book.Title} — {book.Author} ({book.PageCount}p)"
                : $"  {book.Title} — {book.Author} ({book.PageCount}p)  [fallback for \"{slot.Title}\"]");
        }

        var plans = DemoReadingHistory.Build(resolved, today);

        var wantedSoFar = 0;

        foreach (var plan in plans)
        {
            var startedAt = ResolveStartedAt(plan, today);
            var finishedAt = plan.Slot.FinishedDaysAgo is { } daysAgo
                ? today.AddDays(-daysAgo)
                : (DateOnly?)null;

            var addedOn = startedAt?.AddDays(-AddedBeforeStartDays)
                          ?? today.AddDays(-(3 + (wantedSoFar++ * 6)));

            var userBook = new UserBook
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                BookId = plan.Book.Id,
                Status = plan.Slot.Status,
                Rating = plan.Slot.Rating,
                Note = plan.Slot.Note,
                StartPage = plan.StartPage,
                CurrentPage = plan.CurrentPage,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                AddedAt = new DateTimeOffset(addedOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            };

            dbContext.UserBooks.Add(userBook);

            foreach (var entry in plan.Logs)
            {
                dbContext.ReadingLogs.Add(new ReadingLog
                {
                    Id = Guid.NewGuid(),
                    UserBookId = userBook.Id,
                    Date = entry.Date,
                    PagesRead = entry.PagesRead,
                    CreatedAt = new DateTimeOffset(
                        entry.Date.ToDateTime(new TimeOnly(21, 0)), TimeSpan.Zero),
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        Console.WriteLine(
            $"\nSeeded {plans.Count} book(s) and "
            + $"{plans.Sum(plan => plan.Logs.Count)} reading log entries for {Email}.");

        return 0;
    }

    /// <summary>
    /// The day the reading began: the first logged day where there is one, and otherwise a date
    /// worked back from the day the book was finished. A book with no entries still has to say
    /// when it was read — the F3 transition rule fills both dates in when a book is marked as
    /// read, so a demo book without them would not look like anything the application produces.
    /// </summary>
    private static DateOnly? ResolveStartedAt(DemoBookPlan plan, DateOnly today)
    {
        if (plan.Logs.Count > 0)
        {
            return plan.Logs[0].Date;
        }

        return plan.Slot.FinishedDaysAgo is { } daysAgo
            ? today.AddDays(-(daysAgo + UnloggedReadingDays))
            : null;
    }
}
