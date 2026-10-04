using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionViewDataTests
{
    // Wall time and what categories accounted for in it; the difference is what remains.
    private static readonly StartupImpactStageData[] Stages =
    [
        new("LoadingDefs", 1500f, 500f),
        new("StaticConstructorOnStartupCallAll", 27900f, 20000f),
        new("AtlasBaking", 8700f, 8000f),
        new("ResolveReferences", 2900f, 2900f),
    ];

    private static string AfterLoading => StartupImpactSessionViewData.AfterLoadingKey;

    [Test]
    public static void StagesComeLargestFirstWithTheirOwnRemainingTime()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 0f, 0f, 0f);

        Expect.IsTrue(entries.Count == 3);
        Expect.IsTrue(entries[0].Key == "StaticConstructorOnStartupCallAll");
        Expect.IsTrue(entries[0].Ms == 7900f);
        Expect.IsTrue(entries[1].Key == "LoadingDefs");
        Expect.IsTrue(entries[1].Ms == 1000f);
        Expect.IsTrue(entries[2].Key == "AtlasBaking");
        Expect.IsTrue(entries[2].Ms == 700f);
    }

    // A stage whose categories account for all of it (ResolveReferences above) has no entry.
    [Test]
    public static void AStageWithNothingRemainingHasNoEntry()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 0f, 0f, 0f);

        Expect.IsFalse(entries.Any(entry => entry.Key == "ResolveReferences"));
    }

    [Test]
    public static void WhatCameAfterLoadingIsAnEntryOfItsOwn()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 90600f, 110600f, 5900f);

        var afterLoading = entries.Single(entry => entry.Key == AfterLoading);
        Expect.IsTrue(afterLoading.Ms == 14100f);
        Expect.AreEqual(AfterLoading.Translate().ToString(), afterLoading.Label);
        // Larger than any stage's share, so it leads.
        Expect.IsTrue(entries[0].Key == AfterLoading);
    }

    [Test]
    public static void NothingComesAfterLoadingWhenTheMenuWasNeverReached()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 90600f, 0f, 0f);

        Expect.IsFalse(entries.Any(entry => entry.Key == AfterLoading));
    }

    // Twenty seconds after loading, all of it in long events timed under their owners:
    // nothing remains.
    [Test]
    public static void TheLongEventsTimedAfterLoadingLeaveNothingRemaining()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries([], 90600f, 110600f, 20000f);

        Expect.IsEmpty(entries);
    }

    // The second delayed-initialization pass borrows the first one's text everywhere else; in
    // the remaining time both are listed, and two lines with one name read as a mistake.
    [Test]
    public static void TheSecondDelayedInitializationPassIsToldApart()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(
            [
                new(nameof(LoadingStage.ExecuteToExecuteWhenFinished), 1000f, 0f),
                new(nameof(LoadingStage.ExecuteToExecuteWhenFinished2), 500f, 0f),
            ],
            0f,
            0f,
            0f
        );

        Expect.AreEqual(2, entries.Count);
        Expect.AreNotEqual(entries[0].Label, entries[1].Label);
        Expect.IsTrue(entries[1].Label.Contains(entries[0].Label, StringComparison.Ordinal));
    }

    // The bar as the window draws it, from this startup's own session: every entry a segment
    // with a colour, largest first, on a span that holds the remaining total.
    [Test]
    public static void TheRemainingBarCarriesEveryEntryLargestFirstOnASpanHoldingTheTotal()
    {
        var viewData = new StartupImpactSessionViewData(
            StartupImpactSessionData.FromCurrentSession()
        );

        Expect.IsTrue(viewData.CategoriesRemaining.Count == viewData.RemainingByStage.Count);
        Expect.IsTrue(viewData.MetricsRemaining.Count == viewData.RemainingByStage.Count);
        foreach (var category in viewData.CategoriesRemaining)
        {
            Expect.IsTrue(viewData.CategoryColorsRemaining.ContainsKey(category));
        }
        for (var i = 1; i < viewData.MetricsRemaining.Count; i++)
        {
            Expect.IsTrue(viewData.MetricsRemaining[i] <= viewData.MetricsRemaining[i - 1]);
        }
        Expect.GreaterThanOrEqualTo(viewData.RemainingBarSpan, viewData.RemainingLoadingTime);
        Expect.GreaterThanOrEqualTo(viewData.RemainingBarSpan, viewData.MetricsRemaining.Sum());
    }

    // The base game's two bars are drawn against the longer of the two totals, so an
    // off-thread total above the loading-thread one shortens the loading-thread bar.
    [Test]
    public static void TheBaseGameBarsShareTheLongerSpan()
    {
        Expect.IsTrue(StartupImpactSessionViewData.BaseGameBarSpan(21400f, 24300f, true) == 24300f);
        Expect.IsTrue(StartupImpactSessionViewData.BaseGameBarSpan(21400f, 3000f, true) == 21400f);
    }

    [Test]
    public static void WithoutTheOffThreadBarTheLoadingThreadBarSpansItself() =>
        Expect.IsTrue(
            StartupImpactSessionViewData.BaseGameBarSpan(21400f, 24300f, false) == 21400f
        );

    // The mod table's shared scale covers both bars of every visible mod, so a row whose
    // off-thread time is the largest figure in the table sets the scale for all of them; a
    // hidden row does not, however large.
    [Test]
    public static void TheTableScaleCoversBothBarsOfEveryVisibleMod() =>
        Expect.AreApproximatelyEqual(
            3000f,
            StartupImpactSessionViewData.SharedScale([
                (1000f, 3000f, false),
                (2000f, 0f, false),
                (500f, 9000f, true),
            ])
        );
}
