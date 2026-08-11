using System.Text.RegularExpressions;

namespace Bookshelf.Application.Books;

/// <summary>
/// The one place that decides which Open Library subjects are worth keeping, applied when a book
/// is written — never when it is read. Filtering on the way in means the details page, the shelf
/// search and the genre chart all get clean data without any of them knowing this class exists.
///
/// Open Library's subjects are free text typed by volunteers, and a good part of what comes back
/// is not a subject at all but bookkeeping: "nyt:combined-print-and-e-book-fiction=2019-09-29",
/// "Accessible book", "Protected DAISY". Those are what this removes.
///
/// What it deliberately does NOT do is normalise wording: "Fiction", "Fiction, general" and
/// "American fiction" stay three separate tags. Merging them needs a hand-written synonym map
/// that is never finished, which is the reason the decisions document refuses genre filters in
/// the first place.
///
/// IMPORTANT: the seed harvest in the AI phase writes subjects straight into the database from
/// Python, bypassing this code. It must apply the same rule, because the embedding input is
/// "title + author + subjects" and has to be built identically for corpus books and for the
/// reader's own books — otherwise cosine similarity starts measuring how noisy the text was.
///
/// That twin now exists: <c>embedding/seed/subject_filter.py</c>, a faithful port of the rules
/// below (MaxSubjects, JunkPhrases, the namespace-prefix regex, case-insensitive dedup). The
/// duplication is deliberate — the alternative was either an uncleaned corpus or a .NET call in
/// the middle of a Python script. Change one file and the other has to change with it, and the
/// corpus has to be re-seeded, because every stored vector was built from this output.
/// </summary>
public static class SubjectFilter
{
    /// <summary>
    /// Open Library often lists over a hundred tags per book, ordered roughly by relevance; the
    /// first fifteen carry the meaning and the rest is a long tail.
    /// </summary>
    private const int MaxSubjects = 15;

    /// <summary>
    /// Machine bookkeeping rather than subjects. Matched anywhere in the tag, so variants like
    /// "Internet Archive Wishlist" go too.
    /// </summary>
    private static readonly string[] JunkPhrases =
    [
        "accessible book",
        "protected daisy",
        "in library",
        "overdrive",
        "internet archive",
    ];

    /// <summary>
    /// A namespace prefix — "nyt:", "collectionid:", "lc:" — which is how Open Library files
    /// machine data among the subjects. Anchored at the start on purpose: a colon in the middle
    /// of a sentence belongs to a real subject, as in
    /// "Alice (fictitious character : carroll), fiction".
    /// </summary>
    private static readonly Regex NamespacePrefix = new(
        @"^[A-Za-z0-9_-]+:", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Trims, drops the bookkeeping tags, removes duplicates that differ only in case, and keeps
    /// the first <see cref="MaxSubjects"/>. Returns null rather than an empty array, so a book
    /// with nothing left reads the same as a book that never had subjects.
    /// </summary>
    public static string[]? Clean(IEnumerable<string?>? subjects)
    {
        if (subjects is null)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cleaned = subjects
            .Select(subject => subject?.Trim())
            .Where(subject => !string.IsNullOrEmpty(subject))
            .Select(subject => subject!)
            .Where(subject => !IsJunk(subject))
            .Where(seen.Add)
            .Take(MaxSubjects)
            .ToArray();

        return cleaned.Length > 0 ? cleaned : null;
    }

    /// <summary>
    /// Three signs of a machine-written tag: a namespace prefix, an equals sign (no genre has
    /// one, but "nyt:hardcover-fiction=2021-05-23" does), and a URL.
    ///
    /// The rule is deliberately narrow. An earlier, blunter version — "throw away anything with a
    /// colon or a slash" — also swallowed "Alice (fictitious character : carroll), fiction" and
    /// "FICTION / Literary", which are real subjects, and a filter that eats real data to catch
    /// noise is a bad trade. Likewise "anything containing a digit" would take "Fiction, 20th
    /// century" and "World War, 1939-1945" with it.
    /// </summary>
    private static bool IsJunk(string subject)
    {
        if (NamespacePrefix.IsMatch(subject)
            || subject.Contains('=')
            || subject.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var phrase in JunkPhrases)
        {
            if (subject.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
