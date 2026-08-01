using System.Text.Json;
using Bookshelf.Application.Books;
using Bookshelf.Infrastructure.ExternalServices.OpenLibrary.Models;

namespace Bookshelf.Infrastructure.ExternalServices.OpenLibrary;

internal static class OpenLibraryMappingExtensions
{
    private const int MaxSubjects = 15;
    private const string WorksKeyPrefix = "/works/";
    private const string AuthorsKeyPrefix = "/authors/";

    public static BookSearchResult ToBookSearchResult(this OpenLibrarySearchDoc doc) => new(
        StripKeyPrefix(doc.Key, WorksKeyPrefix),
        doc.Title,
        doc.AuthorName is { Count: > 0 } authors ? string.Join(", ", authors) : null,
        doc.FirstPublishYear,
        doc.CoverId,
        doc.NumberOfPagesMedian,
        doc.Subject?.Take(MaxSubjects).ToArray(),
        doc.Isbn?.FirstOrDefault());

    public static OpenLibraryWorkData ToWorkData(this OpenLibraryWorkResponse response) => new(
        response.Title ?? string.Empty,
        ExtractDescription(response.Description),
        response.Authors?
            .Select(a => a.Author?.Key)
            .Where(key => !string.IsNullOrEmpty(key))
            .Select(key => StripKeyPrefix(key!, AuthorsKeyPrefix))
            .ToArray()
            ?? [],
        response.Subjects?.Take(MaxSubjects).ToArray(),
        response.Covers?.FirstOrDefault());

    /// <summary>Open Library returns description either as a plain string or as { "type": ..., "value": ... }.</summary>
    private static string? ExtractDescription(JsonElement? description)
    {
        if (description is not { } value)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object when value.TryGetProperty("value", out var inner) => inner.GetString(),
            _ => null,
        };
    }

    private static string StripKeyPrefix(string key, string prefix) =>
        key.StartsWith(prefix, StringComparison.Ordinal) ? key[prefix.Length..] : key;
}
