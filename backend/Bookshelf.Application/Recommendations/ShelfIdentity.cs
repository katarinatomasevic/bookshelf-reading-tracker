namespace Bookshelf.Application.Recommendations;

/// <summary>
/// Decides whether a candidate book is "the one the reader already has", for recommendation
/// filtering only.
///
/// <para>
/// Excluding books by row id is not enough, and the reason is in Open Library rather than in our
/// code. Open Library holds more than one <em>work</em> record for some titles — Pride and
/// Prejudice exists as both OL66554W and OL15165350W — and the project's dedup rule, fixed in F2,
/// is deliberately "same Open Library key, same book". Two work keys therefore become two rows,
/// legitimately. When the reader owns one of them, the other is still a perfectly valid
/// neighbour, and being nearly the same text it comes back with a similarity around 0.97 — at
/// the very top of the list. The reader is told "because you liked Pride and Prejudice: Pride and
/// Prejudice", which reads as a broken recommender.
/// </para>
///
/// <para>
/// This is a second axis of comparison used <b>only</b> when filtering recommendations. It does
/// not touch the F2 dedup rule: writing a book to the database still matches on
/// <c>OpenLibraryId</c> alone, two rows still exist, and every <c>UserBook</c> still points where
/// it did.
/// </para>
///
/// <para>
/// The comparison is deliberately conservative — lowercase, trimmed, exact. No subtitle
/// stripping, no fuzzy or partial matching. The failure modes are asymmetric: letting an
/// occasional Open Library duplicate slip through costs one odd row in a list of ten, while
/// over-matching silently deletes legitimate recommendations that happen to share an author,
/// and nobody would ever see that happen.
/// </para>
/// </summary>
public static class ShelfIdentity
{
    /// <summary>
    /// The key two books are considered the same by: title and author, lowercased and trimmed.
    /// A missing author collapses to an empty string, so two untitled-author books match each
    /// other and nothing else.
    /// </summary>
    public static string BuildKey(string title, string? author) =>
        $"{Normalize(title)}|{Normalize(author)}";

    private static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;
}
