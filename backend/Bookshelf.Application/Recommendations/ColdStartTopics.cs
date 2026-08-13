namespace Bookshelf.Application.Recommendations;

/// <summary>
/// The order seed topics are drawn in for a reader with nothing on their shelf yet.
///
/// <para>
/// Some fixed order is required rather than merely convenient: an unchanged shelf has to produce
/// an unchanged list, or the "next ten" button would return an arbitrary reshuffle instead of a
/// predictable continuation.
/// </para>
///
/// <para>
/// Two orderings were tried and rejected before this one. <b>Alphabetical</b> put adventure,
/// american literature, ancient history, art and astronomy in the first five — a first screen
/// whose only visible common thread is the letter A. <b>Rotating through every kind of book</b>
/// fixed that but produced a cookbook, a picture book, a photography manual and the Kama Sutra
/// on the opening screen: varied, and unappealing to somebody deciding whether this application
/// is worth their time.
/// </para>
///
/// <para>
/// The reason the second attempt failed is measurement, not taste.
/// <see cref="Domain.Entities.Book.PopularityRank"/> is a position <em>within a topic</em>, so
/// "rank 1 in cooking" and "rank 1 in fantasy" are not comparable numbers — one is the most
/// borrowed cookbook, the other is A Game of Thrones. Nothing in the harvest measures popularity
/// across topics, so the ordering has to supply that judgement itself.
/// </para>
///
/// <para>
/// Hence the shape below: <b>widely read topics first</b>, still rotating between kinds so no two
/// neighbouring entries are the same sort of book, and the narrower topics after them. Taking the
/// first five or the first ten gives mainstream books from different genres. The narrow topics
/// are not removed — a reader who already owns the mainstream picks falls through to them, so
/// the list never runs dry.
/// </para>
///
/// <para>
/// This narrows only <b>what the opening screen is drawn from</b>. Every one of the fifty topics
/// stays in the database and every one of their books remains a candidate for personalised
/// recommendations, which never look at <see cref="Domain.Entities.Book.SeedTopic"/> at all. The
/// warning in <c>embedding/seed/topics.py</c> about narrow topic lists is about corpus coverage,
/// which is untouched.
/// </para>
///
/// <para>
/// Names must match <c>embedding/seed/topics.py</c> exactly, since that is what was written into
/// the database. A topic present there but missing here is not lost: it sorts after everything
/// listed.
/// </para>
/// </summary>
public static class ColdStartTopics
{
    /// <summary>All fifty harvest topics: mainstream reading first, narrower subjects after.</summary>
    private static readonly string[] Order =
    [
        // --- Widely read, rotated between kinds so the opening screen is varied ------------
        "fantasy",
        "mystery",
        "biography",
        "science fiction",
        "historical fiction",
        "psychology",
        "classic literature",
        "thriller",
        "romance",
        "history",
        "horror",
        "young adult fiction",
        "adventure",
        "philosophy",
        "dystopia",

        // --- Everything else, still rotated, for readers who own the picks above -----------
        "graphic novels",
        "short stories",
        "humor",
        "magic realism",
        "true crime",
        "mythology",
        "religion",
        "self-help",
        "business",
        "autobiography",
        "american literature",
        "english literature",
        "russian literature",
        "japanese literature",
        "poetry",
        "drama",
        "children's stories",
        "fairy tales",
        "ancient history",
        "world war ii",
        "politics",
        "economics",
        "feminism",
        "physics",
        "biology",
        "astronomy",
        "mathematics",
        "medicine",
        "computers",
        "cooking",
        "travel",
        "sports",
        "art",
        "music",
        "photography",
    ];

    private static readonly Dictionary<string, int> Positions =
        Order.Select((topic, index) => (topic, index))
            .ToDictionary(entry => entry.topic, entry => entry.index, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where a topic sits in the preferred order. Anything unrecognised sorts to the end rather
    /// than disappearing, so renaming a topic in the harvest degrades the ordering instead of
    /// dropping the books.
    /// </summary>
    public static int RankOf(string topic) =>
        Positions.TryGetValue(topic, out var position) ? position : int.MaxValue;
}
