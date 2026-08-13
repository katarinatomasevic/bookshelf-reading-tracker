namespace Bookshelf.Application.Recommendations;

/// <summary>
/// The ladder the recommender walks down looking for something to base recommendations on. The
/// first rung that actually produces recommendations is the one used.
///
/// <para>
/// The three personalised rungs are the same algorithm with a different input set, not three
/// algorithms — which is the whole point of expressing them as an enum rather than as branches.
/// Popular books are deliberately <em>not</em> a member here: that rung shares no code with the
/// others, takes no source books and produces no attribution, so folding it in would mean an
/// enum value the source-book query could never answer.
/// </para>
/// </summary>
public enum RecommendationTier
{
    /// <summary>
    /// Books the reader rated 4 or 5. The clearest statement of taste there is.
    ///
    /// <para>
    /// Status is deliberately not part of this. F3 allows a rating on any status, so a book rated
    /// 5 while still being read is every bit as strong a signal as one rated 5 after finishing —
    /// requiring "Read" here would silently discard it.
    /// </para>
    /// </summary>
    HighlyRated,

    /// <summary>Everything finished, rated or not. Finishing a book is itself a mild endorsement.</summary>
    Read,

    /// <summary>
    /// Books on the to-read list. Weaker than the others — nothing has been read yet — but
    /// choosing to add a book is still a statement about taste, and it is what a reader who has
    /// just filled their shelf has to offer.
    /// </summary>
    WantToRead,
}
