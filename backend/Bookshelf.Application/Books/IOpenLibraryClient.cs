namespace Bookshelf.Application.Books;

public interface IOpenLibraryClient
{
    Task<BookSearchPageResult> SearchAsync(string query, int page, CancellationToken cancellationToken);

    Task<OpenLibraryWorkData> GetWorkAsync(string workKey, CancellationToken cancellationToken);

    Task<OpenLibraryRatingsData> GetRatingsAsync(string workKey, CancellationToken cancellationToken);

    Task<string> GetAuthorNameAsync(string authorKey, CancellationToken cancellationToken);
}
