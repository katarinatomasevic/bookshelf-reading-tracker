"""Bookshelf embedding service.

A deliberately small FastAPI application whose only job is to turn text into 384-dimension
vectors with all-MiniLM-L6-v2. It is separate from the .NET API for one reason: the sentence
embedding models worth using live in the Python ecosystem, and calling one over HTTP is far less
work than hosting an ONNX runtime inside ASP.NET.

Two things about this file are easy to get wrong and both are done on purpose:

* the model is loaded once, in the lifespan handler, not inside a request. Loading it per request
  would read ~90 MB off disk on every call and make the service unusable under the seed script.
* vectors come back normalised. With unit-length vectors cosine similarity is the dot product,
  which is what pgvector's cosine operator and the recommender both assume. Normalising in one
  place means neither the seed script nor the .NET side can forget to do it.

The service is stateless and knows nothing about books, the database, or who is calling it.
"""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from sentence_transformers import SentenceTransformer

from .schemas import (
    EMBEDDING_DIMENSIONS,
    EmbedBatchRequest,
    EmbedBatchResponse,
    EmbedRequest,
    EmbedResponse,
    HealthResponse,
)

MODEL_NAME = "sentence-transformers/all-MiniLM-L6-v2"

logger = logging.getLogger("bookshelf.embedding")

# Holds the loaded model between startup and shutdown. A module-level variable rather than a
# global getter because there is exactly one model and exactly one process (see the single
# uvicorn worker in the Dockerfile).
_model: SentenceTransformer | None = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Loads the model before the first request is accepted and drops it on shutdown.

    Because uvicorn only starts listening after this yields, a successful /health response is by
    itself proof that the model is in memory — which is what the Docker healthcheck relies on.
    """
    global _model

    logger.info("Loading embedding model %s", MODEL_NAME)
    _model = SentenceTransformer(MODEL_NAME)
    logger.info("Model loaded, serving %d-dimension vectors", EMBEDDING_DIMENSIONS)

    yield

    _model = None


app = FastAPI(
    title="Bookshelf Embedding Service",
    description="Turns book text into 384-dimension vectors for similarity search.",
    lifespan=lifespan,
)


def _encode(texts: list[str]) -> list[list[float]]:
    """Encodes a list of texts in one forward pass.

    normalize_embeddings=True is the single most important argument in this file: it makes every
    returned vector unit length, so cosine distance in Postgres and the similarity numbers shown
    next to a recommendation agree with each other.
    """
    assert _model is not None, "lifespan must have run before any request is served"

    vectors = _model.encode(
        texts,
        normalize_embeddings=True,
        convert_to_numpy=True,
        show_progress_bar=False,
    )

    return vectors.tolist()


@app.get("/health", response_model=HealthResponse)
async def health() -> HealthResponse:
    """Liveness probe for Docker, and a quick way to confirm which model is actually loaded."""
    return HealthResponse(
        status="ok",
        model=MODEL_NAME,
        dimensions=EMBEDDING_DIMENSIONS,
    )


@app.post("/embed", response_model=EmbedResponse)
async def embed(request: EmbedRequest) -> EmbedResponse:
    """Embeds one text. Used by the .NET API when a reader adds a book to their shelf."""
    embedding = _encode([request.text])[0]
    return EmbedResponse(embedding=embedding)


@app.post("/embed/batch", response_model=EmbedBatchResponse)
async def embed_batch(request: EmbedBatchRequest) -> EmbedBatchResponse:
    """Embeds up to MAX_BATCH_SIZE texts at once. Used by the corpus seed script.

    Batching is not just convenience: one forward pass over 256 short texts is several times
    faster than 256 passes over one, which is the difference between a seed that takes a minute
    and one that takes twenty.
    """
    embeddings = _encode(request.texts)
    return EmbedBatchResponse(embeddings=embeddings)
