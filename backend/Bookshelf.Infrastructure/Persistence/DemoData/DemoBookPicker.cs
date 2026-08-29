using Bookshelf.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bookshelf.Infrastructure.Persistence.DemoData;

/// <summary>
/// Turns the slots in <see cref="DemoBooks"/> into real rows of the Book table.
/// <para>
/// Two steps, and the second is the point. The preferred title is looked up first, because a shelf
/// of books the examiner recognises reads better than a shelf assembled by rank. If it is not
/// there, the slot falls back to the best-ranked book of its topic. The corpus is a sample of
/// fifty topics out of Open Library, not a catalogue: of thirty well-known titles checked against
/// it while this was written, six were simply absent. A hard-coded list would therefore be a seed
/// that fails on the one machine it has to work on, and the fallback is what removes that failure
/// mode without giving up on recognisable books.
/// </para>
/// <para>
/// Books are drawn from the corpus rather than fetched from Open Library so that the demo needs no
/// network at all, and so that every book already carries an embedding and the recommendations
/// work the moment the account is opened.
/// </para>
/// </summary>
internal static class DemoBookPicker
{
    public static async Task<IReadOnlyList<(DemoBookSlot Slot, Book Book)>> ResolveAsync(
        AppDbContext dbContext,
        IReadOnlyList<DemoBookSlot> slots,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Books.AnyAsync(book => book.PopularityRank != null, cancellationToken))
        {
            throw new InvalidOperationException(
                "The Book table holds no corpus books, so there is nothing for the demo shelf to "
                + "point at. Seed the corpus first:\n"
                + "    docker compose --profile seed run --rm seed");
        }

        var resolved = new List<(DemoBookSlot, Book)>(slots.Count);
        var taken = new HashSet<Guid>();

        foreach (var slot in slots)
        {
            var book = await FindPreferredAsync(dbContext, slot, taken, cancellationToken)
                       ?? await FindByTopicAsync(dbContext, slot, taken, cancellationToken)
                       ?? throw new InvalidOperationException(
                           $"No book could be found for the demo slot \"{slot.Title}\": it is not "
                           + $"in the corpus, and topic \"{slot.FallbackTopic}\" holds no book of "
                           + $"at least {slot.MinPageCount} pages that is not already on the "
                           + "demo shelf.");

            taken.Add(book.Id);
            resolved.Add((slot, book));
        }

        return resolved;
    }

    /// <summary>
    /// The preferred title. ILIKE without wildcards is a case-insensitive equality test, and the
    /// author is matched as a substring because Open Library routinely lists translators next to
    /// the author. Where several editions share a title, the most widely read one wins.
    /// </summary>
    private static Task<Book?> FindPreferredAsync(
        AppDbContext dbContext,
        DemoBookSlot slot,
        HashSet<Guid> taken,
        CancellationToken cancellationToken) =>
        dbContext.Books
            .Where(book => !taken.Contains(book.Id))
            .Where(book => EF.Functions.ILike(book.Title, slot.Title))
            .Where(book => book.Author != null
                           && EF.Functions.ILike(book.Author, $"%{slot.Author}%"))
            .Where(book => book.PageCount != null && book.PageCount >= slot.MinPageCount)
            .OrderBy(book => book.PopularityRank ?? int.MaxValue)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The fallback: the best-ranked book of the slot's topic that is long enough and not already
    /// spoken for. Restricted to books that carry a rank, which is exactly the corpus — a book a
    /// reader added by hand has none, and has never had its metadata checked by anyone.
    /// </summary>
    private static Task<Book?> FindByTopicAsync(
        AppDbContext dbContext,
        DemoBookSlot slot,
        HashSet<Guid> taken,
        CancellationToken cancellationToken) =>
        dbContext.Books
            .Where(book => !taken.Contains(book.Id))
            .Where(book => book.SeedTopic == slot.FallbackTopic)
            .Where(book => book.PopularityRank != null)
            .Where(book => book.PageCount != null && book.PageCount >= slot.MinPageCount)
            .OrderBy(book => book.PopularityRank)
            .FirstOrDefaultAsync(cancellationToken);
}
