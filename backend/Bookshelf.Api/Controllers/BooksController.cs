using Bookshelf.Api.Extensions;
using Bookshelf.Application.Books;
using Bookshelf.Application.Shelf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController(IBookService bookService, IShelfService shelfService) : ControllerBase
{
    /// <summary>Public, but reads the token when one is sent so results can be marked as already on the shelf.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<BookSearchPageResult>> Search(
        [FromQuery] string q, [FromQuery] int page, CancellationToken cancellationToken)
    {
        var result = await bookService.SearchAsync(
            q, page <= 0 ? 1 : page, User.GetUserIdOrNull(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookDetailsDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var details = await bookService.GetDetailsAsync(id, User.GetUserIdOrNull(), cancellationToken);
        return Ok(details);
    }

    [HttpPost("manual")]
    [Authorize]
    public async Task<ActionResult<ShelfItemDto>> AddManual(
        [FromBody] ManualBookRequest request, CancellationToken cancellationToken)
    {
        var item = await shelfService.AddManualAsync(User.GetUserId(), request, cancellationToken);
        return Ok(item);
    }
}
