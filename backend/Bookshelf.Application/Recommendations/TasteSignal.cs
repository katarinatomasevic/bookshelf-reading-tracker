using Bookshelf.Domain.Entities;
using Pgvector;

namespace Bookshelf.Application.Recommendations;

/// <summary>
/// What the reader's low ratings mean for what they are shown next.
///
/// <para>
/// Everything else in the recommender is built on liking: the tiers pick books a reader rated
/// highly, read, or wanted to read, and a rating of 1 previously did nothing at all except
/// disqualify a book from being a source. The motivating complaint was concrete — rate a book 5
/// in advance on somebody's recommendation, read it, hate it, drop it to 1, and the application
/// keeps offering more by the same writer as though nothing had been said.
/// </para>
///
/// <para>
/// <b>The rule is by author, not by vector, and that is a correction.</b> The first attempt
/// dropped any candidate that sat closer to a disliked book than to the source it was found
/// through. On paper that is the more sophisticated rule; in practice it collapsed. A reader who
/// rated The Shining 1 and It 5 has a disliked book and a liked book that are the same author,
/// the same genre and nearly the same point in the vector space — so every good horror
/// recommendation was about equally close to both, and the comparison threw out roughly half of
/// them for no reason a reader could ever perceive. Ten recommendations became three, and the
/// Stephen King titles the rule existed to remove survived it.
/// </para>
///
/// <para>
/// By author it is predictable and it matches what the reader actually said. Vectors still take
/// part, but only as a <em>penalty</em> on the score — never as a veto — so a candidate that
/// looks very like something the reader rejected sinks down the list instead of vanishing, and
/// the list can no longer collapse.
/// </para>
/// </summary>
public class TasteSignal
{
    /// <summary>Ratings at or below this are a complaint. A 3 is not one.</summary>
    private const int DislikedAtOrBelow = 2;

    /// <summary>Ratings at or above this are an endorsement — the same threshold the first tier uses.</summary>
    private const int LikedAtOrAbove = 4;

    /// <summary>
    /// How hard a resemblance to a rejected book pushes a candidate down. Deliberately larger
    /// than the popularity weight — a reader's explicit "no" should count for more than how
    /// widely borrowed a book is — while still being a nudge rather than a veto.
    /// </summary>
    private const double PenaltyWeight = 0.20;

    private readonly IReadOnlySet<string> rejectedAuthors;
    private readonly IReadOnlyList<Vector> dislikedEmbeddings;

    private TasteSignal(IReadOnlySet<string> rejectedAuthors, IReadOnlyList<Vector> dislikedEmbeddings)
    {
        this.rejectedAuthors = rejectedAuthors;
        this.dislikedEmbeddings = dislikedEmbeddings;
    }

    public static TasteSignal From(IReadOnlyList<RatedBook> rated, Func<string?, string?> primaryAuthor)
    {
        var disliked = rated.Where(book => book.Rating <= DislikedAtOrBelow).ToList();

        // An author the reader has also rated highly is not rejected, however badly one of their
        // books went down. Someone who loved It and hated The Shining has said something about
        // one book, not about Stephen King — and reading it as a verdict on the writer would
        // quietly delete the better half of their own shelf's taste.
        var endorsed = rated
            .Where(book => book.Rating >= LikedAtOrAbove)
            .Select(book => primaryAuthor(book.Author))
            .Where(author => author is not null)
            .Select(author => author!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rejected = disliked
            .Select(book => primaryAuthor(book.Author))
            .Where(author => author is not null && !endorsed.Contains(author))
            .Select(author => author!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new TasteSignal(rejected, disliked.Select(book => book.Embedding).ToList());
    }

    /// <summary>True when this book's author has been rejected outright.</summary>
    public bool IsRejectedAuthor(string? primaryAuthor) =>
        primaryAuthor is not null && rejectedAuthors.Contains(primaryAuthor);

    /// <summary>
    /// How much to subtract from a candidate's score for resembling something the reader
    /// rejected. Zero when nothing has been rejected, which is the usual case.
    ///
    /// <para>
    /// Scaled by similarity to the nearest disliked book rather than applied flatly, so a near
    /// twin of a rejected book is pushed hard and a book that merely shares its genre is barely
    /// touched.
    /// </para>
    /// </summary>
    public double PenaltyFor(Book book)
    {
        if (dislikedEmbeddings.Count == 0 || book.Embedding is not { } embedding)
        {
            return 0;
        }

        var candidate = embedding.Memory.Span;
        var closest = 0.0;

        foreach (var disliked in dislikedEmbeddings)
        {
            var similarity = DotProduct(candidate, disliked.Memory.Span);
            if (similarity > closest)
            {
                closest = similarity;
            }
        }

        return PenaltyWeight * Math.Max(0, closest);
    }

    /// <summary>
    /// Cosine similarity of two vectors, which is just their dot product because the embedding
    /// service returns unit-length vectors — the same property pgvector's cosine operator relies
    /// on, so these numbers are on the same scale as the ones the database returns.
    /// </summary>
    private static double DotProduct(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
        {
            return 0;
        }

        double sum = 0;
        for (var index = 0; index < left.Length; index++)
        {
            sum += left[index] * right[index];
        }

        return sum;
    }
}
