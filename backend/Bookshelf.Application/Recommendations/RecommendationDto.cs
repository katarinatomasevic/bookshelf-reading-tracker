namespace Bookshelf.Application.Recommendations;

/// <summary>
/// Which of the reader's books a recommendation was derived from — the "because you liked …"
/// half of a recommendation card.
///
/// <para>
/// This is not a separate feature bolted onto the algorithm; it is a by-product of its shape.
/// Because each source book is queried on its own, every neighbour that comes back is by
/// definition a neighbour of <em>that</em> book, so the source is already in hand at the moment
/// the candidate appears. (Averaging the reader's vectors into one query, the obvious
/// alternative, would have thrown this away along with the reader's second taste.)
/// </para>
///
/// <para>
/// Worth stating plainly for the defence: this explains a recommendation by naming its nearest
/// neighbour. It does not explain <em>why</em> the two books are close — whether it was the
/// genre, the setting, or the subject tags. That is a real limit, and this should not be
/// described as explainable AI.
/// </para>
/// </summary>
public record BasedOnDto(Guid BookId, string Title);

/// <summary>
/// One recommended book.
///
/// <para>
/// Shaped after <see cref="Books.BookSearchResult"/> so the client can render it with the same
/// card component, with one addition: <see cref="BookId"/>. Every recommendation is by definition
/// already a row in our database — it came out of a vector query against that table — so sending
/// our own id lets "add to shelf" post a bookId and resolve in one lookup instead of going back
/// through Open Library. It is also the only thing that makes a manually added book
/// recommendable at all, since such a book has no Open Library key to send instead.
/// </para>
/// </summary>
public record RecommendationDto(
    Guid BookId,
    string? OpenLibraryId,
    string Title,
    string? Author,
    int? CoverId,
    int? PageCount,
    string[]? Subjects,
    /// <summary>Null for cold-start recommendations, which have no source book to credit.</summary>
    BasedOnDto? BasedOn,
    /// <summary>
    /// Cosine similarity to the source book, 0 for cold start. Returned but deliberately not
    /// rendered: "0.61" means nothing to a reader, while "Because you liked Dune" means
    /// everything. It stays in the payload because it is what the algorithm ranked on, and being
    /// able to show that in Swagger is worth more than hiding it.
    /// </summary>
    double Similarity);

/// <summary>
/// The whole response.
///
/// <para>
/// <see cref="IsColdStart"/> is what tells the client which of two quite different things it is
/// looking at: personalised recommendations, each with a source book, or a starter list of
/// popular books with none. Without it the client would have to infer that from
/// <see cref="RecommendationDto.BasedOn"/> being null on every item — true today, but an
/// inference rather than a statement.
/// </para>
/// </summary>
public record RecommendationsDto(
    IReadOnlyList<RecommendationDto> Items,
    bool IsColdStart);
