namespace Bookshelf.Application.Recommendations;

/// <summary>
/// The order seed topics are drawn in for a reader with nothing on their shelf yet.
///
/// <para>
/// Some fixed order is required rather than merely convenient. There is no "refresh
/// recommendations" button anywhere in the application, on the grounds that recommendations are a
/// pure function of the shelf — so pressing one would redraw an identical list and look broken.
/// That argument only holds if an unchanged shelf really does produce an unchanged list, which
/// rules out picking topics at random.
/// </para>
///
/// <para>
/// The obvious fixed order — alphabetical — was tried and rejected. It works, but the first five
/// topics come out as adventure, american literature, ancient history, art, astronomy: a new
/// reader's very first screen is five books whose only visible common thread is the letter A.
/// The list is meant to look chosen, not sorted.
/// </para>
///
/// <para>
/// So the order below rotates through kinds of book — genre fiction, a life, history, thought,
/// science, poetry, a classic, something practical, something for children, art — before coming
/// back for a second of any kind. Taking the first ten gives ten different kinds; taking the
/// first five still gives five. Which topics they are carries no claim; that they differ from
/// one another is the whole point, and it is what the corpus's fifty-topic breadth was harvested
/// for in the first place.
/// </para>
///
/// <para>
/// The names must match <c>embedding/seed/topics.py</c> exactly, since that is what was written
/// into <see cref="Domain.Entities.Book.SeedTopic"/>. A topic that appears in the database but
/// not here is not lost: it sorts after everything listed, so the corpus stays fully reachable.
/// </para>
/// </summary>
public static class ColdStartTopics
{
    /// <summary>All fifty harvest topics, reordered so that any prefix of this list is varied.</summary>
    private static readonly string[] Order =
    [
        // First pass — ten different kinds of book.
        "fantasy",
        "biography",
        "history",
        "philosophy",
        "astronomy",
        "poetry",
        "classic literature",
        "cooking",
        "children's stories",
        "art",

        // Second pass — a second of each kind, in the same rotation.
        "mystery",
        "autobiography",
        "world war ii",
        "psychology",
        "biology",
        "drama",
        "russian literature",
        "travel",
        "young adult fiction",
        "music",

        // Third pass onwards; kinds drop out of the rotation as they are used up.
        "science fiction",
        "politics",
        "religion",
        "physics",
        "american literature",
        "business",
        "fairy tales",
        "photography",

        "romance",
        "ancient history",
        "mythology",
        "mathematics",
        "english literature",
        "self-help",

        "thriller",
        "economics",
        "medicine",
        "japanese literature",
        "sports",

        "historical fiction",
        "feminism",
        "computers",

        "adventure",
        "true crime",

        // Genre fiction is the largest group, so its tail is what remains.
        "horror",
        "dystopia",
        "magic realism",
        "short stories",
        "graphic novels",
        "humor",
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
