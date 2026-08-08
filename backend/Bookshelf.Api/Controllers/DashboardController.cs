using Bookshelf.Api.Extensions;
using Bookshelf.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Cards, both charts and the streak in one call — they all come from the same data.</summary>
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(
        [FromQuery] string? period,
        [FromQuery] DateOnly? today,
        CancellationToken cancellationToken)
    {
        var dashboard = await dashboardService.GetDashboardAsync(
            User.GetUserId(), period, today, cancellationToken);

        return Ok(dashboard);
    }

    /// <summary>
    /// Separate because the grid always shows the last 26 weeks and the period selector does not
    /// touch it: keeping it here means changing the period does not fetch it again.
    /// </summary>
    [HttpGet("activity")]
    public async Task<ActionResult<ActivityDto>> GetActivity(
        [FromQuery] DateOnly? today,
        CancellationToken cancellationToken)
    {
        var activity = await dashboardService.GetActivityAsync(
            User.GetUserId(), today, cancellationToken);

        return Ok(activity);
    }
}
