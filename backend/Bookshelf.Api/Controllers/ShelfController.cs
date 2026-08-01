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
}
