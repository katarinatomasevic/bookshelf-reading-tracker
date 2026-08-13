namespace Bookshelf.Application.Books;

/// <summary>
/// Builds the text a book is turned into before it is embedded. This is a deliberate, faithful
/// port of <c>embedding/seed/embed_text.py</c>, and the two must produce byte-identical output
/// for identical input.
///
/// <para>
/// The reason is the whole reason this class exists as a class. Cosine similarity compares two
/// vectors, and a sentence transformer builds a vector out of whatever text it is handed. The
/// ~15.6K corpus books were embedded by the Python seed script; if books a reader adds through
/// the application were embedded from anything else — a different separator, a different field,
/// a different order — the two groups would sit in measurably different regions of the vector
/// space. Not because the books differ, but because the inputs did. Similarity would quietly
/// start measuring "how much text was there" instead of "how alike are these books", and the only
/// symptom would be recommendations that feel slightly off. That is a bug nobody finds by reading
/// code.
/// </para>
///
/// <para>
/// <b>No description</b>, even though <see cref="Domain.Entities.Book.Description"/> usually holds
/// one and the details page shows it: books a reader adds have one and corpus books do not, so
/// including it would embed the two groups from visibly different text.
/// </para>
///
/// <para>
/// <b>No title either</b>, and that was measured rather than assumed. Titles were included at
/// first, and over a string this short they dominated everything else: asked for books like
/// <i>The Secret History</i>, the recommender returned The Secret Adversary, Can You Keep A
/// Secret?, Two Can Keep a Secret, The Secret Woman, My Secret Life, Toliver's Secret — ten
/// results out of ten matching on the word "secret" and none on what the books are about. The
/// model was not wrong; it was answering the question the text asked it.
/// </para>
///
/// <para>
/// The objection was that titles are what tie a series together — "Dune" finding Children of Dune
/// and God Emperor of Dune. Tested, and it does not hold: the sequels stay at essentially the
/// same distance without the title (0.9066 against 0.9208 for the nearest), because they share an
/// author and the subject "Dune (Imaginary place)". Dropping the title cost nothing there and
/// fixed the case above.
/// </para>
///
/// <para>
/// Subjects arriving here have already been through <see cref="SubjectFilter"/>, whose Python twin
/// applies the same rules on the other side. Changing either file means changing all three and
/// re-seeding, since every stored vector was built from this output.
/// </para>
/// </summary>
public static class EmbeddingText
{
    /// <summary>
    /// Produces "Author. Subject one, subject two." from a book's fields.
    ///
    /// <para>
    /// Missing parts drop out together with their separator rather than leaving a gap, so a book
    /// with no author reads "Science fiction, space opera." and never ". Science fiction" — an
    /// empty segment would be text the model has to account for, and the corpus never contains
    /// one.
    /// </para>
    ///
    /// <para>
    /// The title is used only when there is nothing else at all — no author and no subjects —
    /// which in the current database is true of exactly one book. Embedding an empty string would
    /// put it at a meaningless point in the vector space and make it a candidate for
    /// recommendations it has no relationship to; its title is poor information, but it is
    /// information.
    /// </para>
    /// </summary>
    public static string Build(string title, string? author, string[]? subjects)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(author))
        {
            parts.Add(author.Trim());
        }

        if (subjects is not null)
        {
            var joined = string.Join(", ", subjects
                .Select(subject => subject.Trim())
                .Where(subject => subject.Length > 0));

            if (joined.Length > 0)
            {
                parts.Add(joined);
            }
        }

        if (parts.Count == 0)
        {
            parts.Add(title.Trim());
        }

        return string.Join(". ", parts.Where(part => part.Length > 0)) + ".";
    }

    /// <summary>Convenience overload: the same text, taken straight off a book.</summary>
    public static string Build(Domain.Entities.Book book) =>
        Build(book.Title, book.Author, book.Subjects);
}
