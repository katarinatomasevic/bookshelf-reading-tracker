namespace Bookshelf.Application.Recommendations;

public interface IRecommendationService
{
    /// <summary>
    /// Recommends up to <paramref name="limit"/> books for a reader, never including books they
    /// already have on their shelf.
    /// </summary>
    /// <param name="offset">
    /// How far into the ranked list to start. This is what "show me different ones" means: the
    /// recommender builds one deterministic list and the reader walks along it, rather than being
    /// handed a reshuffle. Past the end it wraps around to the beginning.
    /// </param>
    Task<RecommendationsDto> GetRecommendationsAsync(
        Guid userId, int limit, int offset, CancellationToken cancellationToken);
}
