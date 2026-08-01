namespace Bookshelf.Application.Books;

public interface IOpenLibraryClient
{
    Task<BookSearchPageResult> SearchAsync(string query, int page, CancellationToken cancellationToken);

    /// <summary>
    /// The search document for one known work. Page count and ISBN live only in the search
    /// index — the work endpoint carries neither — so this is the way to reach them for a
    /// book the client did not arrive at through a search.
    /// </summary>
    Task<BookSearchResult?> GetByWorkKeyAsync(string workKey, CancellationToken cancellationToken);

    Task<OpenLibraryWorkData> GetWorkAsync(string workKey, CancellationToken cancellationToken);

    Task<OpenLibraryRatingsData> GetRatingsAsync(string workKey, CancellationToken cancellationToken);

    Task<string> GetAuthorNameAsync(string authorKey, CancellationToken cancellationToken);
}
