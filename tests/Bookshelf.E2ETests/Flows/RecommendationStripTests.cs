using Bookshelf.E2ETests.Support;

namespace Bookshelf.E2ETests.Flows;

/// <summary>
/// Flow 5 — the recommendation strip at the foot of the shelf.
///
/// <para>
/// The plan wrote this flow as "open recommendations", meaning the <c>/recommendations</c> route.
/// Phase 9 removed that route: it was the same grid as the strip behind a second address, and the
/// strip is now the only place recommendations live. The flow is therefore tested where it
/// actually is.
/// </para>
/// <para>
/// A brand new reader has an empty shelf, which puts the recommender on the fourth rung of F5 —
/// cold start, drawn from the seeded corpus and labelled "Popular" because there is no book on
/// the shelf to attribute a suggestion to. That makes it the one recommendation state a test can
/// assert on without first building up a reading history.
/// </para>
/// </summary>
[TestFixture]
[Category("E2E")]
public class RecommendationStripTests : E2ETestBase
{
    [Test]
    public void A_new_reader_is_offered_popular_books()
    {
        RegisterNewReader("coldstart");

        Shelf.Open();
        Recommendations.WaitUntilLoaded();

        Assert.That(
            Recommendations.IsShown(),
            Is.True,
            "The strip is shown under an empty shelf too — that is where cold start earns its keep.");

        RequireSeededCorpus();

        Assert.Multiple(() =>
        {
            Assert.That(Recommendations.IsColdStart(), Is.True);
            Assert.That(Recommendations.Heading(), Does.Contain("Popular"));
            Assert.That(
                Recommendations.CardCount(),
                Is.GreaterThan(0),
                "Cold start should fill the strip rather than leave it empty.");
        });
    }

    /// <summary>
    /// Cold start carries no "because you liked X" — there is nothing on the shelf to have liked.
    /// Every card is marked as popular instead.
    /// </summary>
    [Test]
    public void Cold_start_cards_are_marked_popular_rather_than_attributed()
    {
        RegisterNewReader("popular");

        Shelf.Open();
        Recommendations.WaitUntilLoaded();
        RequireSeededCorpus();

        var reasons = Recommendations.Reasons();

        Assert.That(reasons, Is.Not.Empty);
        Assert.That(
            reasons,
            Has.All.Contains("Popular"),
            "With an empty shelf there is no source book to attribute anything to.");
    }

    /// <summary>
    /// The refresh button added in Phase 9 asks for the next slice rather than the same one. The
    /// original objection — that a deterministic list makes the button look broken — held for a
    /// button that re-requested the same ten, not for this one.
    /// </summary>
    [Test]
    public void Asking_for_different_books_returns_different_books()
    {
        RegisterNewReader("refresh");

        Shelf.Open();
        Recommendations.WaitUntilLoaded();
        RequireSeededCorpus();

        var first = Recommendations.Titles();
        Assume.That(first, Is.Not.Empty);

        Recommendations.ShowDifferentBooks();
        var second = Recommendations.Titles();

        Assert.That(
            second,
            Is.Not.EqualTo(first),
            "'Show different books' asks for the next offset, so the list has to change.");
    }

    /// <summary>
    /// Cold start draws from the seeded corpus. An unseeded database is a setup problem, not a
    /// defect, and saying so is more useful than an assertion failure about an empty strip.
    /// </summary>
    private void RequireSeededCorpus()
    {
        if (Recommendations.CardCount() == 0)
        {
            Assert.Inconclusive(
                "The recommendation strip is empty, which for a new reader means the seed corpus "
                    + "is missing. Run the Phase 8 seed first: "
                    + "docker compose --profile seed run --rm seed");
        }
    }
}
