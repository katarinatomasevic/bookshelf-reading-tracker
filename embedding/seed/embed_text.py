"""The one definition of what text a book turns into before it is embedded.

This is the most load-bearing twenty lines in the AI phase, and the reason is not obvious.

Cosine similarity compares two vectors, and a sentence transformer builds a vector out of
whatever text it is handed. If corpus books were embedded from "title + author + subjects" while
the reader's own books were embedded from "title + author + subjects + a 300-word description",
the two groups would sit in measurably different regions of the vector space — not because the
books differ, but because the inputs had different length and register. Similarity would quietly
start measuring "how much text was there" instead of "how alike are these books", and the only
symptom would be recommendations that feel slightly off. That is a bug nobody finds by reading
code.

Hence: no description, even though the database stores one and the details page shows it. The
practical argument points the same way — Open Library's search API does not return descriptions,
so including them would mean ~10K extra requests to /works/{id}.json.

Subjects are a good substitute anyway. For Dune they read "science fiction, space opera, desert,
ecology, politics" — dense, and often more informative than marketing copy.

THE C# SIDE MUST MATCH THIS EXACTLY. When the API embeds a book a reader adds
(Bookshelf.Infrastructure, AI recommender phase), it has to produce a byte-identical string for
identical input. Any change here is a change there, and invalidates every vector already stored.
"""


def build_embedding_text(
    title: str,
    author: str | None,
    subjects: list[str] | None,
) -> str:
    """Builds "Title. Author. Subject one, subject two." from a book's fields.

    Missing parts drop out with their separator rather than leaving an empty gap, so a book with
    no author reads "Dune. Science fiction, space opera." and not "Dune. . Science fiction".
    Title is the only required part; a book without one should never have been harvested.
    """
    parts: list[str] = [title.strip()]

    if author and author.strip():
        parts.append(author.strip())

    if subjects:
        joined = ", ".join(subject.strip() for subject in subjects if subject.strip())
        if joined:
            parts.append(joined)

    return ". ".join(part for part in parts if part) + "."
