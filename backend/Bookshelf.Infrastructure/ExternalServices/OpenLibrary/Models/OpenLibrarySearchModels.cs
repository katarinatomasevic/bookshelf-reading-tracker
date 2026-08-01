using System.Text.Json.Serialization;

namespace Bookshelf.Infrastructure.ExternalServices.OpenLibrary.Models;

internal sealed record OpenLibrarySearchResponse(
    [property: JsonPropertyName("numFound")] int NumFound,
    [property: JsonPropertyName("docs")] List<OpenLibrarySearchDoc>? Docs);

internal sealed record OpenLibrarySearchDoc(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author_name")] List<string>? AuthorName,
    [property: JsonPropertyName("first_publish_year")] int? FirstPublishYear,
    [property: JsonPropertyName("cover_i")] int? CoverId,
    [property: JsonPropertyName("number_of_pages_median")] int? NumberOfPagesMedian,
    [property: JsonPropertyName("subject")] List<string>? Subject,
    [property: JsonPropertyName("isbn")] List<string>? Isbn);
