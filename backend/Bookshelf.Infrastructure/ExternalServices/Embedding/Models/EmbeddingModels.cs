using System.Text.Json.Serialization;

namespace Bookshelf.Infrastructure.ExternalServices.Embedding.Models;

/// <summary>
/// Wire shapes for the Python embedding service, mirroring <c>embedding/app/schemas.py</c>.
/// Property names are spelled out explicitly rather than left to a naming policy, because these
/// have to match another codebase that will not be recompiled alongside this one.
/// </summary>
public record EmbedRequest([property: JsonPropertyName("text")] string Text);

public record EmbedResponse([property: JsonPropertyName("embedding")] float[]? Embedding);

public record EmbedBatchRequest([property: JsonPropertyName("texts")] IReadOnlyList<string> Texts);

public record EmbedBatchResponse([property: JsonPropertyName("embeddings")] float[][]? Embeddings);
