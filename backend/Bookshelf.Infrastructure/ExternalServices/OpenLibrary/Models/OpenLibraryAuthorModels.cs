using System.Text.Json.Serialization;

namespace Bookshelf.Infrastructure.ExternalServices.OpenLibrary.Models;

internal sealed record OpenLibraryAuthorResponse(
    [property: JsonPropertyName("name")] string? Name);
