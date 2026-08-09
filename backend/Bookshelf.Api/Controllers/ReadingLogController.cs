using Bookshelf.Api.Extensions;
using Bookshelf.Application.ReadingLogs;
using Bookshelf.Application.Shelf;
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

    /// <summary>
    /// The reading history of one book. Always for a single book — a history of everything would
    /// be the activity chart, which the dashboard already draws.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReadingLogDto>>> GetHistory(
        [FromQuery] Guid userBookId, CancellationToken cancellationToken)
    {
        var history = await readingLogService.GetHistoryAsync(
            User.GetUserId(), userBookId, cancellationToken);

        return Ok(history);
    }

    /// <summary>
    /// Both of these return the updated shelf entry, because correcting the past moves the
    /// reader's current position and the client needs the new one.
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ShelfItemDto>> Update(
        Guid id, [FromBody] UpdateReadingLogRequest request, CancellationToken cancellationToken)
    {
        var item = await readingLogService.UpdateAsync(
            User.GetUserId(), id, request, cancellationToken);

        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ShelfItemDto>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var item = await readingLogService.DeleteAsync(User.GetUserId(), id, cancellationToken);

        return Ok(item);
    }
}
