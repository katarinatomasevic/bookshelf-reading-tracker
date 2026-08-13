namespace Bookshelf.Application.Books;

/// <summary>
/// Builds the text a book is turned into before it is embedded. This is a deliberate, faithful
/// port of <c>embedding/seed/embed_text.py</c>, and the two must produce byte-identical output
/// for identical input.
///
/// <para>
/// The reason is the whole reason this class exists as a class. Cosine similarity compares two
/// vectors, and a sentence transformer builds a vector out of whatever text it is handed. The
/// ~10K corpus books were embedded by the Python seed script from "title + author + subjects".
/// If books a reader adds through the application were embedded from anything else — a different
/// separator, a different subject order, a description appended — the two groups would sit in
/// measurably different regions of the vector space. Not because the books differ, but because
/// the inputs did. Similarity would quietly start measuring "how much text was there" instead of
/// "how alike are these books", and the only symptom would be recommendations that feel slightly
/// off. That is a bug nobody finds by reading code.
/// </para>
///
/// <para>
/// Hence no description, even though <see cref="Domain.Entities.Book.Description"/> usually holds
/// one and the details page shows it. Subjects carry the signal anyway: for Dune they read
/// "science fiction, space opera, desert, ecology, politics", which is denser and often more
/// informative than marketing copy.
/// </para>
///
/// <para>
/// Subjects arriving here have already been through <see cref="SubjectFilter"/>, because that
/// filter runs when a book is written, never when it is read. The Python side applies the same
/// rules through its own port in <c>embedding/seed/subject_filter.py</c>. Changing either file
/// means changing all three and re-seeding, since every stored vector was built from this output.
/// </para>
/// </summary>
public static class EmbeddingText
{
    /// <summary>
    /// Produces "Title. Author. Subject one, subject two." from a book's fields.
    ///
    /// <para>
    /// Missing parts drop out together with their separator rather than leaving a gap, so a book
    /// with no author reads "Dune. Science fiction, space opera." and never "Dune. . Science
    /// fiction" — an empty segment would be text the model has to account for, and the corpus
    /// never contains one.
    /// </para>
    /// </summary>
    public static string Build(string title, string? author, string[]? subjects)
    {
        var parts = new List<string>();

        // Python appends the title unconditionally and only filters empties at the join, so a
        // book with a blank title yields "." there. Matched exactly rather than "improved":
        // a book without a title should never have been harvested, and quietly diverging here
        // is precisely the class of difference this port exists to prevent.
        parts.Add(title.Trim());

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

        return string.Join(". ", parts.Where(part => part.Length > 0)) + ".";
    }

    /// <summary>Convenience overload: the same text, taken straight off a book.</summary>
    public static string Build(Domain.Entities.Book book) =>
        Build(book.Title, book.Author, book.Subjects);
}
