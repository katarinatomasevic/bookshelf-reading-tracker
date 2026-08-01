namespace Bookshelf.Application.Books;

public interface IBookService
{
    Task<BookSearchPageResult> SearchAsync(
        string query, int page, Guid? userId, CancellationToken cancellationToken);

    Task<BookDetailsDto> GetDetailsAsync(string id, Guid? userId, CancellationToken cancellationToken);
}
