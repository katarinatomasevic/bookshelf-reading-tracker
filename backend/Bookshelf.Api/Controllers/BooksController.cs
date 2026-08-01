using Bookshelf.Application.Books;
using Microsoft.AspNetCore.Mvc;

namespace Bookshelf.Api.Controllers;

[ApiController]
[Route("api/books")]
public class BooksController(IBookService bookService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<BookSearchPageResult>> Search(
        [FromQuery] string q, [FromQuery] int page, CancellationToken cancellationToken)
    {
        var result = await bookService.SearchAsync(q, page <= 0 ? 1 : page, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookDetailsDto>> GetById(string id, CancellationToken cancellationToken)
    {
        var details = await bookService.GetDetailsAsync(id, cancellationToken);
        return Ok(details);
    }
}
