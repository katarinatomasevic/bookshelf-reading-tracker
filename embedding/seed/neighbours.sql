-- Manual sanity check of the vector corpus, and the closest thing to a unit test the AI
-- infrastructure has before the recommender exists. Run it after seeding:
--
--   docker compose exec db psql -U bookshelf -d bookshelf -f /dev/stdin < embedding/seed/neighbours.sql
--
-- or paste a query into psql directly.
--
-- The operator is pgvector's <=>, cosine distance: 0 means identical direction, 1 means
-- unrelated, 2 means opposite. Because the embedding service returns unit-length vectors,
-- (1 - distance) is exactly the cosine similarity the recommender will show next to a
-- recommendation, so the numbers here are the same numbers the user interface will display.
--
-- What "working" looks like: the neighbours of a fantasy novel are other fantasy novels, the
-- neighbours of a cookbook are other cookbooks. What it is NOT: an explanation. Nearest-neighbour
-- search says two books are close, never why — which is the honest limit of this whole approach
-- and should be stated as such rather than dressed up as explainable AI.

-- 1. Corpus health: how much of the table has a vector, and how many topics are represented.
SELECT
    count(*)                                              AS books,
    count(*) FILTER (WHERE "Embedding" IS NOT NULL)       AS with_embedding,
    count(*) FILTER (WHERE "SeedTopic" IS NOT NULL)       AS from_corpus,
    count(DISTINCT "SeedTopic")                           AS distinct_topics
FROM "Book";

-- 2. Nearest neighbours of one book, by title.
--    Change the title on the third line to try another book.
WITH source AS (
    SELECT "Id", "Title", "Embedding"
    FROM "Book"
    WHERE "Title" = 'The Hobbit' AND "Embedding" IS NOT NULL
    LIMIT 1
)
SELECT
    candidate."Title",
    candidate."Author",
    candidate."SeedTopic",
    round((1 - (candidate."Embedding" <=> source."Embedding"))::numeric, 4) AS similarity
FROM "Book" AS candidate
CROSS JOIN source
WHERE candidate."Id" <> source."Id"
  AND candidate."Embedding" IS NOT NULL
ORDER BY candidate."Embedding" <=> source."Embedding"
LIMIT 10;

-- 3. Negative control, and the more convincing half of the check. If a cookbook's nearest
--    neighbours were also fantasy novels, section 2 would prove nothing — every vector would
--    simply be close to everything. Two different starting points giving two different
--    neighbourhoods is what shows the vectors carry real signal.
WITH source AS (
    SELECT "Id", "Title", "Embedding"
    FROM "Book"
    WHERE "SeedTopic" = 'cooking' AND "Embedding" IS NOT NULL
    ORDER BY "PopularityRank"
    LIMIT 1
)
SELECT
    source."Title" AS started_from,
    candidate."Title",
    candidate."SeedTopic",
    round((1 - (candidate."Embedding" <=> source."Embedding"))::numeric, 4) AS similarity
FROM "Book" AS candidate
CROSS JOIN source
WHERE candidate."Id" <> source."Id"
  AND candidate."Embedding" IS NOT NULL
ORDER BY candidate."Embedding" <=> source."Embedding"
LIMIT 10;

-- 4. Proof that the HNSW index is actually used, for the defence.
--    Expect "Index Scan using IX_Book_Embedding". Note that over ~10K rows the planner may
--    reasonably prefer a sequential scan, which is not a fault: an exact scan of 10K vectors
--    takes about ten milliseconds. The index is here because pgvector is part of the course,
--    not because performance demanded it — claiming otherwise would not survive a follow-up
--    question. Force it with `SET enable_seqscan = off;` if you want to see the index plan.
EXPLAIN ANALYZE
SELECT "Id"
FROM "Book"
WHERE "Embedding" IS NOT NULL
ORDER BY "Embedding" <=> (SELECT "Embedding" FROM "Book" WHERE "Embedding" IS NOT NULL LIMIT 1)
LIMIT 10;
