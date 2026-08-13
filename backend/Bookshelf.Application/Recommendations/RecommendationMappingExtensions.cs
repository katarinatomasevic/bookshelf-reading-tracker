using Bookshelf.Domain.Entities;

namespace Bookshelf.Application.Recommendations;

/// <summary>
/// Hand-written mapping from entity to DTO, as everywhere else in this project — no mapping
/// library, so what crosses the boundary is visible in one readable place.
/// </summary>
public static class RecommendationMappingExtensions
{
    /// <summary>A personalised recommendation, credited to the book it was found through.</summary>
    public static RecommendationDto ToRecommendationDto(
        this Book book, RecommendationSource source, double similarity) =>
        new(
            book.Id,
            book.OpenLibraryId,
            book.Title,
            book.Author,
            book.CoverId,
            book.PageCount,
            book.Subjects,
            new BasedOnDto(source.BookId, source.Title),
            similarity);

    /// <summary>
    /// A cold-start recommendation. No source and no similarity, because nothing was compared:
    /// this book is here because it is popular, and the card says exactly that.
    /// </summary>
    public static RecommendationDto ToPopularRecommendationDto(this Book book) =>
        new(
            book.Id,
            book.OpenLibraryId,
            book.Title,
            book.Author,
            book.CoverId,
            book.PageCount,
            book.Subjects,
            null,
            0);
}
