namespace Bookshelf.Application.Recommendations;

public interface IRecommendationService
{
    /// <summary>
    /// Recommends up to <paramref name="limit"/> books for a reader, never including books they
    /// already have on their shelf.
    /// </summary>
    Task<RecommendationsDto> GetRecommendationsAsync(
        Guid userId, int limit, CancellationToken cancellationToken);
}
