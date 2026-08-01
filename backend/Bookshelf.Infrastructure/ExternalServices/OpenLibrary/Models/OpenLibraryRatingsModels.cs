using System.Text.Json.Serialization;

namespace Bookshelf.Infrastructure.ExternalServices.OpenLibrary.Models;

internal sealed record OpenLibraryRatingsResponse(
    [property: JsonPropertyName("summary")] OpenLibraryRatingsSummary? Summary);

internal sealed record OpenLibraryRatingsSummary(
    [property: JsonPropertyName("average")] double? Average,
    [property: JsonPropertyName("count")] int? Count);
