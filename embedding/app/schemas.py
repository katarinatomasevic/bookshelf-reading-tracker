"""Request and response models for the embedding service.

Validation lives here rather than in the endpoint bodies so that a bad request is rejected by
FastAPI with a 422 before the model is ever touched.
"""

from pydantic import BaseModel, Field

# Width of all-MiniLM-L6-v2's output, and therefore of the Book.Embedding column, which is
# declared as vector(384). Both numbers describe the same model and have to change together.
EMBEDDING_DIMENSIONS = 384

# Upper bound on how many texts one request may carry. Without it the seed script would post all
# ~10K corpus entries in a single call and the container would run out of memory part-way
# through; with it the caller is forced to chunk, which also gives useful progress output.
MAX_BATCH_SIZE = 256


class EmbedRequest(BaseModel):
    """A single text to embed."""

    text: str = Field(min_length=1)


class EmbedResponse(BaseModel):
    embedding: list[float]


class EmbedBatchRequest(BaseModel):
    """Up to MAX_BATCH_SIZE texts, embedded in one forward pass."""

    texts: list[str] = Field(min_length=1, max_length=MAX_BATCH_SIZE)


class EmbedBatchResponse(BaseModel):
    """Embeddings in the same order as the submitted texts — the caller matches them by index."""

    embeddings: list[list[float]]


class HealthResponse(BaseModel):
    status: str
    model: str
    dimensions: int
