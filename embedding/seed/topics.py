"""The 50 Open Library subjects the corpus is harvested from.

Three things depend on this list, which is why it is a versioned source file and not a command
line argument:

1. **Coverage.** A recommendation can only ever be a book that is in the corpus. A list of forty
   flavours of fantasy would produce a recommender that is excellent at fantasy and blind to
   everything else, and the blindness would be invisible during testing.

2. **Cold start.** With an empty shelf the recommender shows one book from each of ten different
   SeedTopic values. Those ten topics are drawn from this list, so if the list is narrow, a new
   reader's very first impression of the application is ten near-identical books.

3. **Reproducibility.** The order below is the harvest order, and a book that appears under
   several subjects keeps the first one it was seen under. Fixed order therefore means that
   running the harvest twice produces the same corpus, with the same topics and the same ranks.

The grouping comments are for human eyes only; the harvest just walks the list top to bottom.
Deliberately mixed: genre fiction and literary fiction, fiction and non-fiction, entertainment
and study, and a few subjects (poetry, mythology, fairy tales) that no genre-only list would
reach.
"""

TOPICS: list[str] = [
    # --- Genre fiction -------------------------------------------------------------------
    "fantasy",
    "science fiction",
    "mystery",
    "thriller",
    "horror",
    "romance",
    "historical fiction",
    "adventure",
    "dystopia",
    "magic realism",
    "short stories",
    "graphic novels",
    "humor",
    # --- Literary and classic ------------------------------------------------------------
    "classic literature",
    "american literature",
    "english literature",
    "russian literature",
    "japanese literature",
    # --- Poetry and drama ----------------------------------------------------------------
    "poetry",
    "drama",
    # --- Younger readers -----------------------------------------------------------------
    "young adult fiction",
    "children's stories",
    "fairy tales",
    # --- Lives ---------------------------------------------------------------------------
    "biography",
    "autobiography",
    # --- History and society -------------------------------------------------------------
    "history",
    "ancient history",
    "world war ii",
    "politics",
    "economics",
    "feminism",
    # Replaces a "science" topic that had to be dropped. The relevance filter matches whole words,
    # and "science" is a whole word inside "Science fiction" — so that topic quietly collected
    # novels instead of science, while physics, biology, astronomy, mathematics, medicine and
    # computers already cover the subject properly. True crime fills the slot with something the
    # list was genuinely missing rather than with another flavour of what it already had.
    "true crime",
    # --- Thought -------------------------------------------------------------------------
    "philosophy",
    "psychology",
    "religion",
    "mythology",
    # --- Science and technology ----------------------------------------------------------
    "physics",
    "biology",
    "astronomy",
    "mathematics",
    "medicine",
    "computers",
    # --- Practical and lifestyle ---------------------------------------------------------
    "self-help",
    "business",
    "cooking",
    "travel",
    "sports",
    # --- Arts ----------------------------------------------------------------------------
    "art",
    "music",
    "photography",
]

assert len(TOPICS) == len(set(TOPICS)), "TOPICS must not contain duplicates"
