"""Builds corpus.json from Open Library's search API.

Run on the host, outside Compose:

    python -m seed.harvest --probe     # one page per topic, prints a report, writes nothing
    python -m seed.harvest             # the real thing, ~20-40 minutes

Why the corpus comes from the search API and not from a Kaggle dataset or an Open Library dump:

* Deduplication across the whole application keys on OpenLibraryId. Goodbooks-10k and the UCSD
  Goodreads sets carry no Open Library identifier, so a reader could take a recommendation, add
  it, later find the same book through search, and add it a second time — two rows, no way to
  merge them.
* The works dump is ~10 GB and, more to the point, contains no popularity signal at all.

The search API has both: ?sort=readinglog orders by how many people have the book on a shelf,
which is exactly the measure of popularity the cold start needs, and it comes back already
sorted, so PopularityRank is just the position in the response.

Harvesting is kept separate from embedding on purpose. This script depends on the network, takes
half an hour and can die in the middle; embedding 10K texts on a CPU takes about a minute. Split
in two, the expensive half runs once and seeding stays reproducible and quick.
"""

from __future__ import annotations

import argparse
import functools
import json
import os
import re
import sys
import time
import urllib.parse
from pathlib import Path
from typing import Any

import requests

from .subject_filter import clean
from .topics import TOPICS

BASE_URL = "https://openlibrary.org/search.json"

# 100 is the largest page size the search API serves.
PAGE_SIZE = 100

# Six pages per topic. The plan assumed three, which would have been right without the relevance
# filter below — but that filter rejects roughly half of every page (measured across all 50
# topics: 50% mean yield), so three pages would have produced about 5K books instead of the 10K
# the decisions document asks for. Six pages across 50 topics is ~15,000 relevant rows before
# deduplication, landing near 10K unique.
#
# 10K rather than 20K, as decided: the reader looks at the top ten recommendations and never at
# the top five hundred, so a second half-hour of harvesting buys nothing.
PAGES_PER_TOPIC = 6

# Only the fields that are actually used, so Open Library does not serialise a hundred columns
# per row. number_of_pages_median is the search API's page count; there is no plain page field.
FIELDS = ",".join([
    "key",
    "title",
    "author_name",
    "subject",
    "number_of_pages_median",
    "cover_i",
    "isbn",
])

# Open Library asks callers to identify themselves. Harvesting 150 pages anonymously is a
# reliable way to get rate limited half way through.
DEFAULT_USER_AGENT = "Bookshelf-SeminarProject/1.0 (student project)"

# Deliberately unhurried. The harvest is a background chore that runs a handful of times in the
# project's life, and being a good citizen of a free volunteer-run API costs nothing here.
DELAY_BETWEEN_REQUESTS_SECONDS = 1.0

REQUEST_TIMEOUT_SECONDS = 60
MAX_ATTEMPTS = 4

REPO_ROOT = Path(__file__).resolve().parents[2]
CORPUS_PATH = REPO_ROOT / "corpus.json"


@functools.lru_cache(maxsize=None)
def _topic_pattern(topic: str) -> re.Pattern[str]:
    """Whole-word matcher for a topic, compiled once per topic.

    Word boundaries rather than a plain substring, because several topics are short words that
    live inside unrelated ones: "art" appears in "Earth", "Martian" and "heart", and a substring
    match would file half the corpus under art.
    """
    return re.compile(r"\b" + re.escape(topic) + r"\b", re.IGNORECASE)


def is_topical(topic: str, subjects: list[str]) -> bool:
    """Whether a book genuinely belongs to the topic it was returned under.

    This filter is not in the decisions document, and it is here because the assumption the
    document rests on turned out to be false. The plan expected
    ?subject={topic}&sort=readinglog to return popular books *about* that topic. Measurement
    showed it returns the globally most-shelved books that carry the tag *anywhere* in their
    subject list — and popular books accumulate hundreds of volunteer-typed tags. Hamlet lists
    142 subjects, one of which is "Mathematics, study and teaching", so Hamlet came back as the
    top result for mathematics. Travel returned A Game of Thrones; Japanese literature returned
    Crime and Punishment.

    Two things break without this filter, and the second is the serious one:

    * the corpus collapses, because every topic returns roughly the same few hundred bestsellers
    * SeedTopic becomes false. A book keeps the first topic it was seen under, so Hamlet would sit
      in the database as SeedTopic='mathematics'. Cold start shows one book from each of ten
      different SeedTopic values, so a new reader's first impression would be Hamlet presented as
      the representative book about mathematics.

    The match runs against the *cleaned* subjects — the fifteen that are stored and embedded —
    not against all 142. That is the whole point: the embedding is built from those fifteen, so if
    the topic is not among them the book does not sit near that topic in vector space either, and
    a SeedTopic claiming otherwise contradicts the vector it is attached to. The filter is really
    just a demand that the metadata agree with the maths.

    Measured cost: about half of each page is rejected, so PAGES_PER_TOPIC doubled. Measured
    benefit: russian literature went from O Alquimista and Twilight to Crime and Punishment, Anna
    Karenina and The Brothers Karamazov.
    """
    pattern = _topic_pattern(topic)
    return any(pattern.search(subject) for subject in subjects)


def _session() -> requests.Session:
    session = requests.Session()
    session.headers["User-Agent"] = os.environ.get("HARVEST_USER_AGENT", DEFAULT_USER_AGENT)
    return session


def _fetch_page(session: requests.Session, topic: str, page: int) -> list[dict[str, Any]]:
    """Fetches one page of a topic, retrying with a widening delay.

    sort=readinglog is a genuinely expensive query on Open Library's side and regularly takes
    ten seconds or more, so timeouts here are normal rather than exceptional and are worth
    retrying instead of aborting a half-finished harvest.
    """
    params = {
        "subject": topic,
        "sort": "readinglog",
        "limit": PAGE_SIZE,
        "page": page,
        "fields": FIELDS,
    }
    url = f"{BASE_URL}?{urllib.parse.urlencode(params)}"

    for attempt in range(1, MAX_ATTEMPTS + 1):
        try:
            response = session.get(url, timeout=REQUEST_TIMEOUT_SECONDS)
            response.raise_for_status()
            return response.json().get("docs", [])
        except (requests.RequestException, ValueError) as error:
            if attempt == MAX_ATTEMPTS:
                print(f"    giving up on '{topic}' page {page}: {error}", file=sys.stderr)
                return []
            backoff = 2 ** attempt
            print(f"    attempt {attempt} failed ({error}); retrying in {backoff}s", file=sys.stderr)
            time.sleep(backoff)

    return []


def _to_entry(doc: dict[str, Any], topic: str, rank: int) -> dict[str, Any] | None:
    """Maps one search result to a corpus entry, or None if the book is not worth keeping.

    Three rejections, all for the same reason — a book that cannot be embedded meaningfully is
    dead weight in a corpus whose only purpose is similarity search:

    * no work key: nothing to deduplicate on, and no way to reach the book later
    * no title: the embedding text would be almost empty
    * no subjects left after filtering: title and author alone carry very little signal, so the
      vector would sit in a vague nowhere and pollute everyone's neighbours

    ...and a fourth, is_topical, which rejects a book that carries the topic somewhere in its
    long tail of tags but not among the fifteen that are actually kept. See that function for
    why, and for what the corpus looks like without it.

    Missing covers and missing page counts are NOT a reason to drop a book. They only make the
    card look plainer; they say nothing about how similar the book is to anything else.
    """
    key = doc.get("key") or ""
    open_library_id = key.rsplit("/", 1)[-1] if key.startswith("/works/") else ""
    if not open_library_id:
        return None

    title = (doc.get("title") or "").strip()
    if not title:
        return None

    subjects = clean(doc.get("subject"))
    if not subjects:
        return None

    if not is_topical(topic, subjects):
        return None

    # Open Library returns a list of authors; the application has no author page and no search by
    # author, so joining them into one string is enough (the same choice the Book schema makes).
    author_names = doc.get("author_name") or []
    author = ", ".join(author_names[:3]) if author_names else None

    isbn_list = doc.get("isbn") or []

    return {
        "openLibraryId": open_library_id,
        "title": title,
        "author": author,
        "subjects": subjects,
        "pageCount": doc.get("number_of_pages_median"),
        "coverId": doc.get("cover_i"),
        "isbn": isbn_list[0] if isbn_list else None,
        # Position inside this topic, continuing across pages: page 2 ranks 101-200. The cold
        # start orders by this within a topic, so it has to reflect Open Library's own ordering.
        "popularityRank": rank,
        "seedTopic": topic,
    }


def _write_corpus(entries: dict[str, dict[str, Any]]) -> None:
    """Writes corpus.json atomically, via a temporary file and a rename.

    Called after every topic rather than once at the end. A harvest that dies on topic 41 then
    leaves 40 topics' worth of usable corpus behind instead of nothing, and the atomic rename
    means an interrupted write can never leave a half-written JSON file in its place.
    """
    temporary = CORPUS_PATH.with_suffix(".json.tmp")
    with temporary.open("w", encoding="utf-8") as handle:
        json.dump(list(entries.values()), handle, ensure_ascii=False, indent=1)
    temporary.replace(CORPUS_PATH)


def _load_existing() -> dict[str, dict[str, Any]]:
    """Reads a previous corpus.json so an interrupted harvest can resume instead of restarting.

    Resuming preserves first-seen wins: entries already on disk keep the topic and rank they were
    harvested under, and topics that already contributed are skipped entirely.
    """
    if not CORPUS_PATH.exists():
        return {}

    with CORPUS_PATH.open(encoding="utf-8") as handle:
        entries = json.load(handle)

    return {entry["openLibraryId"]: entry for entry in entries}


def probe(session: requests.Session) -> int:
    """Requests a single page per topic and reports what came back.

    Worth the fifty requests: a subject that Open Library spells differently ("world war ii" vs
    "World War, 1939-1945") returns nothing, and finding that out now costs a minute, whereas
    finding it out from the finished corpus costs the whole harvest.
    """
    print(f"Probing {len(TOPICS)} topics, one page each\n")
    print(f"{'topic':<24}{'returned':>9}{'kept':>6}{'yield':>7}  examples")
    print("-" * 100)

    empty: list[str] = []
    total_kept = 0

    for topic in TOPICS:
        docs = _fetch_page(session, topic, page=1)
        kept = [entry for index, doc in enumerate(docs, start=1)
                if (entry := _to_entry(doc, topic, index)) is not None]

        if not kept:
            empty.append(topic)
        total_kept += len(kept)

        percentage = (len(kept) / len(docs) * 100) if docs else 0
        examples = "; ".join(entry["title"][:26] for entry in kept[:3])
        print(f"{topic:<24}{len(docs):>9}{len(kept):>6}{percentage:>6.0f}%  {examples}")
        time.sleep(DELAY_BETWEEN_REQUESTS_SECONDS)

    print("-" * 100)

    if empty:
        print(f"\nTopics that returned nothing usable: {', '.join(empty)}")
        print("Fix their spelling in topics.py before running the real harvest.")
        return 1

    # A rough forecast so the size of the corpus is known before committing to the harvest rather
    # than after it. Later pages hold less popular books with fewer tags and tend to yield a bit
    # less than page one, and deduplication across topics removes more still, so treat this as an
    # optimistic ceiling rather than a promise.
    projected = total_kept * PAGES_PER_TOPIC
    print(f"\nEvery topic returned results.")
    print(f"Page 1 across all topics kept {total_kept} books "
          f"({total_kept / len(TOPICS):.0f} per topic on average).")
    print(f"At {PAGES_PER_TOPIC} pages per topic that projects to roughly {projected} rows "
          f"before deduplication.")
    return 0


def harvest(session: requests.Session) -> int:
    entries = _load_existing()
    already_harvested = {entry["seedTopic"] for entry in entries.values()}

    if entries:
        print(f"Resuming: {len(entries)} books already in corpus.json "
              f"from {len(already_harvested)} topic(s)\n")

    for position, topic in enumerate(TOPICS, start=1):
        if topic in already_harvested:
            print(f"[{position}/{len(TOPICS)}] {topic}: already harvested, skipping")
            continue

        before = len(entries)
        rank = 0

        for page in range(1, PAGES_PER_TOPIC + 1):
            docs = _fetch_page(session, topic, page)
            if not docs:
                break

            for doc in docs:
                # Rank counts books that passed the filter, not raw response positions. With the
                # relevance filter rejecting about half of every page, raw positions would leave
                # ranks like 1, 4, 9, 14 and would be numbering books that were never stored.
                # Counting survivors instead makes PopularityRank mean "the Nth most-shelved book
                # that is genuinely about this topic", which is what cold start reads it as.
                #
                # Gaps can still appear in the stored data, for a different and harmless reason:
                # a book claimed by an earlier topic keeps that topic (first sighting wins) but
                # still consumes a rank here, because it really is the Nth most popular book of
                # this topic too. Cold start orders by the column and takes the first row, so
                # gaps cost nothing.
                entry = _to_entry(doc, topic, rank + 1)
                if entry is None:
                    continue

                rank += 1

                # First sighting wins. A book listed under both "science fiction" and "space
                # opera" keeps the topic it was met under first, which — because TOPICS has a
                # fixed order — makes the whole harvest deterministic. Overwriting instead would
                # hand every popular book to whichever topic happened to come last.
                entries.setdefault(entry["openLibraryId"], entry)

            time.sleep(DELAY_BETWEEN_REQUESTS_SECONDS)

        _write_corpus(entries)
        added = len(entries) - before
        print(f"[{position}/{len(TOPICS)}] {topic}: +{added} new (total {len(entries)})")

    print(f"\nWrote {len(entries)} unique books to {CORPUS_PATH}")
    return 0


def _force_utf8_output() -> None:
    """Makes stdout and stderr survive non-English book titles.

    This script runs on the host, and on Windows the console defaults to cp1252, which cannot
    encode most of what Open Library returns — a Japanese or Cyrillic title in the progress
    output is enough to kill the whole harvest with a UnicodeEncodeError half an hour in. The
    corpus file itself was never at risk (json.dump writes UTF-8 explicitly); it was purely the
    progress printing, which is a wretched reason to lose a harvest.
    """
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            # line_buffering as well as the encoding: a harvest that prints its progress only
            # when a 8 KB buffer happens to fill is indistinguishable from one that has hung,
            # and this runs for the better part of an hour.
            stream.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)


def main() -> int:
    _force_utf8_output()

    parser = argparse.ArgumentParser(description="Harvest the Bookshelf seed corpus.")
    parser.add_argument(
        "--probe",
        action="store_true",
        help="Fetch one page per topic and report coverage without writing corpus.json.",
    )
    arguments = parser.parse_args()

    session = _session()
    return probe(session) if arguments.probe else harvest(session)


if __name__ == "__main__":
    sys.exit(main())
