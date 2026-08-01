namespace Bookshelf.Application.Books;

public interface IBookService
{
    Task<BookSearchPageResult> SearchAsync(string query, int page, CancellationToken cancellationToken);

    Task<BookDetailsDto> GetDetailsAsync(string id, CancellationToken cancellationToken);
}
