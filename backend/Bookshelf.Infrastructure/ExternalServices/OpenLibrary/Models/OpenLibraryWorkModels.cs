using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bookshelf.Infrastructure.ExternalServices.OpenLibrary.Models;

internal sealed record OpenLibraryWorkResponse(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] JsonElement? Description,
    [property: JsonPropertyName("subjects")] List<string>? Subjects,
    [property: JsonPropertyName("covers")] List<int>? Covers,
    [property: JsonPropertyName("authors")] List<OpenLibraryWorkAuthorRef>? Authors);

internal sealed record OpenLibraryWorkAuthorRef(
    [property: JsonPropertyName("author")] OpenLibraryKeyRef? Author);

internal sealed record OpenLibraryKeyRef(
    [property: JsonPropertyName("key")] string? Key);
