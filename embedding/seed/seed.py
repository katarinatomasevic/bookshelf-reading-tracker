"""Loads corpus.json into the Book table and fills in the embeddings.

Run through Compose, which supplies the database URL and the embedding service address:

    docker compose --profile seed run --rm seed

The script is idempotent by construction, and that is the property to demonstrate: running it a
second time inserts nothing, embeds nothing, and prints zeroes. Two separate mechanisms get it
there, and they cover different cases:

* the insert uses ON CONFLICT DO NOTHING on OpenLibraryId, so a book already in the table — put
  there by an earlier seed, or by a reader who added it through the application — is left exactly
  as it is. It keeps its own Id, so every UserBook row pointing at it stays valid.
* embedding is a separate pass over "every book whose Embedding is still NULL", not something
  bolted onto the insert. That is what makes the two cases converge: a book the reader added
  before the corpus existed has no vector, is skipped by the insert, and would stay invisible to
  the recommender forever if embedding only ever happened on insert. Sweeping NULLs instead
  fixes it on the next seed.

The two passes are deliberately not one transaction each way. The insert is atomic; the
embedding pass commits in batches, because it makes a network call per batch and there is no
value in throwing away four thousand computed vectors because the last request timed out.
"""

from __future__ import annotations

import json
import os
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import psycopg
import requests
from pgvector.psycopg import register_vector
from pgvector.utils import Vector

from .embed_text import build_embedding_text

# Matches the service's own limit; the service rejects anything larger with a 422.
EMBED_BATCH_SIZE = 256

# Rows per insert round trip. Large enough that 10K books take a few round trips rather than ten
# thousand, small enough not to build a multi-megabyte statement in memory.
INSERT_BATCH_SIZE = 1000

EMBED_TIMEOUT_SECONDS = 120


def _require_env(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        print(f"Environment variable {name} is not set.", file=sys.stderr)
        raise SystemExit(1)
    return value


def _load_corpus(path: Path) -> list[dict[str, Any]]:
    if not path.exists():
        print(
            f"{path} does not exist. Run the harvest first, on the host:\n"
            f"    cd embedding && python -m seed.harvest",
            file=sys.stderr,
        )
        raise SystemExit(1)

    with path.open(encoding="utf-8") as handle:
        corpus = json.load(handle)

    print(f"Read {len(corpus)} books from {path}")
    return corpus


def insert_books(connection: psycopg.Connection, corpus: list[dict[str, Any]]) -> int:
    """Inserts every corpus book that is not already in the table. Returns how many were added.

    Note the WHERE clause on the conflict target. Book's unique index on OpenLibraryId is partial
    — it only covers rows where the column is not null, so that the many manually added books
    with no Open Library key do not all collide on a single NULL. Postgres will only infer a
    partial index if the statement repeats its predicate, and without it this fails outright with
    "no unique or exclusion constraint matching the ON CONFLICT specification".

    Embedding is left NULL here and filled by the pass below.
    """
    statement = """
        INSERT INTO "Book" (
            "Id", "OpenLibraryId", "Title", "Author", "Description",
            "CoverId", "PageCount", "Isbn", "Subjects", "CreatedAt",
            "PopularityRank", "SeedTopic"
        )
        VALUES (%s, %s, %s, %s, NULL, %s, %s, %s, %s, %s, %s, %s)
        ON CONFLICT ("OpenLibraryId") WHERE "OpenLibraryId" IS NOT NULL DO NOTHING
    """

    now = datetime.now(timezone.utc)
    inserted = 0

    with connection.cursor() as cursor:
        for start in range(0, len(corpus), INSERT_BATCH_SIZE):
            batch = corpus[start:start + INSERT_BATCH_SIZE]
            parameters = [
                (
                    uuid.uuid4(),
                    entry["openLibraryId"],
                    entry["title"],
                    entry.get("author"),
                    entry.get("coverId"),
                    entry.get("pageCount"),
                    entry.get("isbn"),
                    entry.get("subjects"),
                    now,
                    entry.get("popularityRank"),
                    entry.get("seedTopic"),
                )
                for entry in batch
            ]

            cursor.executemany(statement, parameters)
            inserted += cursor.rowcount if cursor.rowcount and cursor.rowcount > 0 else 0
            print(f"  inserted up to book {min(start + INSERT_BATCH_SIZE, len(corpus))}"
                  f"/{len(corpus)}")

    connection.commit()
    return inserted


def _embed(embedding_url: str, texts: list[str]) -> list[list[float]]:
    response = requests.post(
        f"{embedding_url.rstrip('/')}/embed/batch",
        json={"texts": texts},
        timeout=EMBED_TIMEOUT_SECONDS,
    )
    response.raise_for_status()
    return response.json()["embeddings"]


def fill_missing_embeddings(connection: psycopg.Connection, embedding_url: str) -> int:
    """Embeds every book that still has no vector. Returns how many were filled.

    The text comes from the database rather than from corpus.json on purpose: the same code then
    handles corpus books and books a reader added themselves, and there is only one answer to
    "what was this vector built from". Description is not part of it even though the column often
    holds one — see embed_text.py for why mixing input shapes quietly corrupts similarity.
    """
    with connection.cursor() as cursor:
        cursor.execute("""
            SELECT "Id", "Title", "Author", "Subjects"
            FROM "Book"
            WHERE "Embedding" IS NULL
            ORDER BY "CreatedAt"
        """)
        pending = cursor.fetchall()

    if not pending:
        print("No books are missing an embedding.")
        return 0

    print(f"Embedding {len(pending)} book(s) in batches of {EMBED_BATCH_SIZE}")
    filled = 0

    with connection.cursor() as cursor:
        for start in range(0, len(pending), EMBED_BATCH_SIZE):
            batch = pending[start:start + EMBED_BATCH_SIZE]
            texts = [build_embedding_text(title, author, subjects)
                     for _, title, author, subjects in batch]

            vectors = _embed(embedding_url, texts)

            cursor.executemany(
                'UPDATE "Book" SET "Embedding" = %s WHERE "Id" = %s',
                # Wrapped in Vector rather than passed as a plain list: register_vector teaches
                # psycopg about pgvector's own type and about numpy arrays, but not about lists.
                # A bare list of floats would be adapted as float8[] and rejected by Postgres as
                # the wrong type for a vector(384) column.
                [(Vector(vector), book_id) for vector, (book_id, *_) in zip(vectors, batch)],
            )
            # Committed per batch: a network failure on batch forty should not discard the
            # thirty-nine batches that already succeeded.
            connection.commit()

            filled += len(batch)
            print(f"  embedded {filled}/{len(pending)}")

    return filled


def main() -> int:
    database_url = _require_env("SEED_DATABASE_URL")
    embedding_url = _require_env("EMBEDDING_URL")
    corpus_path = Path(os.environ.get("CORPUS_PATH", "/app/corpus.json"))

    corpus = _load_corpus(corpus_path)

    with psycopg.connect(database_url) as connection:
        # Looks up the vector type's OID in this database and teaches psycopg to read and write
        # it. Without it nothing can touch Book.Embedding at all.
        register_vector(connection)

        print("\nInserting books")
        inserted = insert_books(connection, corpus)

        print("\nFilling in embeddings")
        filled = fill_missing_embeddings(connection, embedding_url)

        with connection.cursor() as cursor:
            cursor.execute('SELECT count(*) FROM "Book"')
            total = cursor.fetchone()[0]
            cursor.execute('SELECT count(*) FROM "Book" WHERE "Embedding" IS NOT NULL')
            embedded = cursor.fetchone()[0]

    print(
        f"\nDone. Inserted {inserted} new book(s), embedded {filled}.\n"
        f"Book now holds {total} row(s), {embedded} of them with a vector."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
