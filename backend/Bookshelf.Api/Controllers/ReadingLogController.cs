using Bookshelf.Api.Extensions;
using Bookshelf.Application.ReadingLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/reading-log")]
[Authorize]
public class ReadingLogController(IReadingLogService readingLogService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<LogProgressResponse>> LogProgress(
        [FromBody] LogProgressRequest request, CancellationToken cancellationToken)
    {
        var response = await readingLogService.LogProgressAsync(
            User.GetUserId(), request, cancellationToken);

        return Ok(response);
    }
}
