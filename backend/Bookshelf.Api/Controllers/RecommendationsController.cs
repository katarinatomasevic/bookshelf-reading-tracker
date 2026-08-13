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
    /// Books to read next, as a window onto one deterministic ranked list.
    ///
    /// <para>
    /// No cache header and no server-side cache: the answer must change the moment the shelf
    /// does. A cached list would mean rating a book, coming back, and seeing no difference, which
    /// reads as a bug.
    /// </para>
    ///
    /// <para>
    /// <paramref name="offset"/> is what makes a refresh button honest. The original decision was
    /// to have no such button, reasoning that the result is a pure function of the shelf and a
    /// click would redraw the identical list. That reasoning holds for a button that asks for the
    /// same ten — so this one asks for the <em>next</em> ten instead, and the objection goes away
    /// without giving up determinism: the same shelf and the same offset always produce the same
    /// books.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<RecommendationsDto>> Get(
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var recommendations = await recommendationService.GetRecommendationsAsync(
            User.GetUserId(), limit, offset, cancellationToken);

        return Ok(recommendations);
    }
}
