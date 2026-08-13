using Bookshelf.Api.Extensions;
using Bookshelf.Application.Recommendations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController(IRecommendationService recommendationService) : ControllerBase
{
    /// <summary>
    /// Books to read next. Ten for the recommendations page, five for the strip at the bottom of
    /// the shelf — the caller says which, since it is the same list either way.
    /// <para>
    /// No cache header and no server-side cache: the answer must change the moment the shelf
    /// does. It is also why there is no "refresh" button anywhere in the interface — the result
    /// is a pure function of the shelf, so pressing one would redraw the identical list and look
    /// broken.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<RecommendationsDto>> Get(
        [FromQuery] int limit = 10, CancellationToken cancellationToken = default)
    {
        var recommendations = await recommendationService.GetRecommendationsAsync(
            User.GetUserId(), limit, cancellationToken);

        return Ok(recommendations);
    }
}
