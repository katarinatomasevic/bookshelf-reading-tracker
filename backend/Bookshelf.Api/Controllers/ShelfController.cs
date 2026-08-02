using Bookshelf.Api.Extensions;
using Bookshelf.Application.Shelf;
using Bookshelf.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/shelf")]
[Authorize]
public class ShelfController(IShelfService shelfService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ShelfItemDto>> Add(
        [FromBody] AddToShelfRequest request, CancellationToken cancellationToken)
    {
        var item = await shelfService.AddAsync(User.GetUserId(), request, cancellationToken);
        return Ok(item);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShelfItemDto>>> GetShelf(
        [FromQuery] ReadingStatus? status,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        var shelf = await shelfService.GetShelfAsync(User.GetUserId(), status, sort, cancellationToken);
        return Ok(shelf);
    }

    [HttpGet("counts")]
    public async Task<ActionResult<ShelfCountsDto>> GetCounts(CancellationToken cancellationToken)
    {
        var counts = await shelfService.GetCountsAsync(User.GetUserId(), cancellationToken);
        return Ok(counts);
    }

    [HttpPatch("{userBookId:guid}")]
    public async Task<ActionResult<ShelfItemDto>> Update(
        Guid userBookId,
        [FromBody] UpdateUserBookRequest request,
        CancellationToken cancellationToken)
    {
        var item = await shelfService.UpdateAsync(User.GetUserId(), userBookId, request, cancellationToken);
        return Ok(item);
    }

    [HttpDelete("{userBookId:guid}")]
    public async Task<IActionResult> Remove(Guid userBookId, CancellationToken cancellationToken)
    {
        await shelfService.RemoveAsync(User.GetUserId(), userBookId, cancellationToken);
        return NoContent();
    }
}
