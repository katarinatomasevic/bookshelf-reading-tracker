"""Python twin of Bookshelf.Application.Books.SubjectFilter.

The harvest writes subjects straight into the database from Python, bypassing the C# code that
cleans them everywhere else. That leaves two copies of one rule, which is normally a smell — but
the alternative is worse: either the corpus keeps Open Library's raw tags while the reader's own
books get cleaned ones, or the harvest has to call into .NET. The first breaks the embedding
(see embed_text.py for why identical input matters), the second means a whole service for a
script that runs twice a year.

So the rule is duplicated deliberately and both files point at each other. If either changes,
the other has to change with it, and the corpus has to be re-seeded.

Kept faithful to the C# original, including what it deliberately does NOT do: it never rewrites
wording, so "Fiction", "Fiction, general" and "American fiction" stay three separate tags.
"""

import re

# Open Library often lists over a hundred tags per book, roughly by relevance; the first fifteen
# carry the meaning and the rest is a long tail.
MAX_SUBJECTS = 15

# Machine bookkeeping rather than subjects. Matched anywhere in the tag, so variants like
# "Internet Archive Wishlist" go too.
JUNK_PHRASES = (
    "accessible book",
    "protected daisy",
    "in library",
    "overdrive",
    "internet archive",
)

# A namespace prefix — "nyt:", "collectionid:", "lc:" — which is how Open Library files machine
# data among the subjects. Anchored at the start on purpose: a colon in the middle of a sentence
# belongs to a real subject, as in "Alice (fictitious character : carroll), fiction".
NAMESPACE_PREFIX = re.compile(r"^[A-Za-z0-9_-]+:")


def _is_junk(subject: str) -> bool:
    """Three signs of a machine-written tag: a namespace prefix, an equals sign (no genre has one,
    but "nyt:hardcover-fiction=2021-05-23" does), and a URL.

    The rule is deliberately narrow. A blunter version — "throw away anything with a colon or a
    slash" — also swallows "Alice (fictitious character : carroll), fiction" and "FICTION /
    Literary", which are real subjects, and a filter that eats real data to catch noise is a bad
    trade.
    """
    if NAMESPACE_PREFIX.match(subject) or "=" in subject or "http" in subject.lower():
        return True

    lowered = subject.lower()
    return any(phrase in lowered for phrase in JUNK_PHRASES)


def clean(subjects: list[str] | None) -> list[str] | None:
    """Trims, drops the bookkeeping tags, removes case-insensitive duplicates, keeps the first
    MAX_SUBJECTS. Returns None rather than an empty list, so a book with nothing left reads the
    same as a book that never had subjects.
    """
    if subjects is None:
        return None

    seen: set[str] = set()
    cleaned: list[str] = []

    for subject in subjects:
        if not isinstance(subject, str):
            continue

        trimmed = subject.strip()
        if not trimmed or _is_junk(trimmed):
            continue

        key = trimmed.lower()
        if key in seen:
            continue

        seen.add(key)
        cleaned.append(trimmed)

        if len(cleaned) == MAX_SUBJECTS:
            break

    return cleaned or None
