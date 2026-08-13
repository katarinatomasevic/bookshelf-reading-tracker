using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Recommendations;

/// <summary>
/// Turns a book's popularity into a number the ranking can add to its similarity.
///
/// <para>
/// Exists because similarity alone produced recommendations that were technically close and
/// practically odd. The text a book is embedded from is "title. author. subjects.", which in so
/// short a string means the title carries real weight — so a reader who liked <i>The Secret
/// History</i> was offered a run of obscure books whose only connection was the word "secret" in
/// the title. Similarity could not tell those apart from good matches; popularity can.
/// </para>
///
/// <para>
/// Deliberately a <b>small additive term</b>, not a multiplier and not a co-equal ranking key.
/// Similarity stays the primary signal and popularity decides between candidates that are already
/// close, pushing obscure books down. A reader should never be handed an unrelated book because
/// it is famous.
/// </para>
/// </summary>
public static class PopularityScore
{
    /// <summary>
    /// How much popularity may move a candidate. Chosen against the spread actually observed in
    /// this corpus: similarities among the top candidates for one source book typically differ by
    /// 0.05–0.20, so a term whose whole range is about 0.06 can reorder near-ties and cannot
    /// overturn a genuine similarity gap.
    /// </summary>
    public const double Weight = 0.08;

    /// <summary>
    /// Popularity of a book with no rank but a real Open Library record. Slightly below the
    /// middle of the scale rather than at the bottom: a missing rank means "was not in our
    /// fifty-topic harvest", not "obscure". The corpus is a sample of a library with millions of
    /// works, so well-known books are missing from it all the time.
    /// </summary>
    private const double UnrankedOpenLibraryScore = 0.40;

    /// <summary>
    /// Popularity of a manually entered book — one with no Open Library key at all. Lower than
    /// any corpus book, because nothing about it has been checked: one reader typed the title and
    /// author, and it may be a duplicate or a misspelling of a book that already exists.
    /// <para>
    /// The accepted consequence is that books readers add themselves are recommended to others
    /// less often. That is a preference in ranking, not a barrier: search runs against the whole
    /// of Open Library and is untouched by any of this, and it — not the recommender — is how a
    /// reader looks for a particular book.
    /// </para>
    /// </summary>
    private const double ManualEntryScore = 0.25;

    /// <summary>
    /// Maps rank to a 0–1 score. Logarithmic because the difference between rank 1 and rank 10
    /// matters and the difference between rank 200 and rank 300 does not: rank 1 scores 1.0,
    /// rank 10 scores 0.5, and everything past a hundred flattens out near 0.3.
    /// </summary>
    public static double Of(Book book)
    {
        if (book.PopularityRank is not { } rank)
        {
            return book.OpenLibraryId is null ? ManualEntryScore : UnrankedOpenLibraryScore;
        }

        // Guards against a rank of 0 or below, which would send the logarithm to infinity or
        // negative. Harvested ranks are 1-based, so this is defensive rather than expected.
        var safeRank = Math.Max(rank, 1);

        return 1.0 / (1.0 + Math.Log10(safeRank));
    }
}
