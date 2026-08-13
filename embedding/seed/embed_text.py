"""The one definition of what text a book turns into before it is embedded.

This is the most load-bearing twenty lines in the AI phase, and the reason is not obvious.

Cosine similarity compares two vectors, and a sentence transformer builds a vector out of
whatever text it is handed. If corpus books were embedded from one shape of text while the
reader's own books were embedded from another, the two groups would sit in measurably different
regions of the vector space — not because the books differ, but because the inputs had different
length and register. Similarity would quietly start measuring "how much text was there" instead
of "how alike are these books", and the only symptom would be recommendations that feel slightly
off. That is a bug nobody finds by reading code.

THE C# SIDE MUST MATCH THIS EXACTLY. When the API embeds a book a reader adds
(Bookshelf.Application/Books/EmbeddingText.cs), it has to produce a byte-identical string for
identical input. Any change here is a change there, and invalidates every vector already stored.

WHAT IS NOT IN THE TEXT, AND WHY
--------------------------------

No description, even though the database stores one and the details page shows it. The practical
argument is that Open Library's search API does not return descriptions, so including them would
mean ~15K extra requests to /works/{id}.json — but the real argument is the one above: books a
reader adds *do* have descriptions and corpus books do not, so including them would embed the two
groups from visibly different text.

No title either, and that one was measured rather than assumed. Titles were included at first,
and over a string this short they dominated: asked for books like "The Secret History", the
recommender returned The Secret Adversary, Can You Keep A Secret?, Two Can Keep a Secret, The
Secret Woman, My Secret Life, Toliver's Secret — ten out of ten matched on the word "secret" and
none on what the books are about. The model was not wrong; it was answering the question the text
asked it.

The obvious objection was that titles are what tie a series together — "Dune" finding Children of
Dune and God Emperor of Dune. That was tested too, and it does not hold: the sequels stay at
essentially the same distance without the title (0.9066 against 0.9208 for the nearest), because
they share an author and the subject "Dune (Imaginary place)". Dropping the title cost nothing
there and fixed the case above.

What is left is author and subjects, which is what "similar" should mean for a book. Subjects
carry it well: for Dune they read "science fiction, space opera, desert, ecology, politics" —
dense, and often more informative than marketing copy.

Subjects arriving here have already been cleaned by subject_filter.py, whose C# twin does the
same on the other side.
"""


def build_embedding_text(
    title: str,
    author: str | None,
    subjects: list[str] | None,
) -> str:
    """Builds "Author. Subject one, subject two." from a book's fields.

    Missing parts drop out with their separator rather than leaving an empty gap, so a book with
    no author reads "Science fiction, space opera." and not ". Science fiction".

    The title is used only when there is nothing else at all — no author and no subjects — which
    in the current database is true of exactly one book. Embedding an empty string would place it
    at a meaningless point in the vector space and make it a candidate for recommendations it has
    no relationship to; its title is poor information, but it is information.
    """
    parts: list[str] = []

    if author and author.strip():
        parts.append(author.strip())

    if subjects:
        joined = ", ".join(subject.strip() for subject in subjects if subject.strip())
        if joined:
            parts.append(joined)

    if not parts:
        parts.append(title.strip())

    return ". ".join(part for part in parts if part) + "."
