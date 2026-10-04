using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StageLedgerTests
{
    [Test]
    public static void TimeBeforeTheFirstStageChangeBelongsToTheInitialStage()
    {
        var ledger = new StageLedger("Initializing");
        ledger.Attribute(100f, 500f);
        ledger.Begin("LoadModXml", 1000f);
        ledger.Close(1500f);

        var entries = ledger.Entries;
        Expect.AreEqual(2, entries.Count);
        Expect.AreEqual("Initializing", entries[0].Stage);
        Expect.AreApproximatelyEqual(1000f, entries[0].WallMs);
        Expect.AreApproximatelyEqual(100f, entries[0].AttributedMs);
        Expect.AreApproximatelyEqual(900f, entries[0].RemainingMs);
    }

    [Test]
    public static void AStageRunsUntilTheNextOneBegins()
    {
        var ledger = new StageLedger("Initializing");
        ledger.Begin("LoadModXml", 1000f);
        ledger.Attribute(250f, 1500f);
        ledger.Begin("CombineIntoUnifiedXml", 1600f);
        ledger.Attribute(50f, 1690f);
        ledger.Close(1700f);

        var entries = ledger.Entries;
        Expect.AreEqual(3, entries.Count);
        Expect.AreEqual("LoadModXml", entries[1].Stage);
        Expect.AreApproximatelyEqual(600f, entries[1].WallMs);
        Expect.AreApproximatelyEqual(250f, entries[1].AttributedMs);
        Expect.AreApproximatelyEqual(100f, entries[2].WallMs);
        Expect.AreApproximatelyEqual(50f, entries[2].AttributedMs);
    }

    // A category that starts in one stage and stops in the next credits each stage with the
    // part of it that ran there. Crediting it all to the stage it stopped in left the first
    // stage looking unaccounted for.
    [Test]
    public static void ACategoryCrossingAStageBoundaryIsSplitBetweenTheStages()
    {
        var ledger = new StageLedger("Initializing");
        ledger.Begin("ResolveCrossReferencesBetweenNonImpliedDefsStage1", 1000f);
        ledger.Begin("ResolveCrossReferencesBetweenNonImpliedDefsStage2", 1300f);
        ledger.Attribute(330f, 1330f);
        ledger.Close(1340f);

        var entries = ledger.Entries;
        Expect.AreApproximatelyEqual(300f, entries[1].AttributedMs);
        Expect.AreApproximatelyEqual(0f, entries[1].RemainingMs);
        Expect.AreApproximatelyEqual(30f, entries[2].AttributedMs);
        Expect.AreApproximatelyEqual(10f, entries[2].RemainingMs);
    }

    // A category timed for longer than the stages it overlapped reads as nothing remaining
    // there, never as a negative amount.
    [Test]
    public static void RemainingTimeNeverGoesNegative()
    {
        var ledger = new StageLedger("Initializing");
        ledger.Begin("LoadModXml", 1000f);
        ledger.Attribute(5000f, 1100f);
        ledger.Close(1100f);

        Expect.AreApproximatelyEqual(0f, ledger.Entries[0].RemainingMs);
        Expect.AreApproximatelyEqual(0f, ledger.Entries[1].RemainingMs);
    }

    [Test]
    public static void NothingIsRecordedAfterClosing()
    {
        var ledger = new StageLedger("Initializing");
        ledger.Begin("LoadModXml", 1000f);
        ledger.Close(2000f);
        ledger.Begin("Finished", 3000f);
        ledger.Attribute(400f, 3500f);

        var entries = ledger.Entries;
        Expect.AreEqual(2, entries.Count);
        Expect.AreApproximatelyEqual(1000f, entries[1].WallMs);
        Expect.AreApproximatelyEqual(0f, entries[1].AttributedMs);
    }

    [Test]
    public static void AnOpenStageHasNoWallTimeYet()
    {
        var ledger = new StageLedger("Initializing");
        ledger.Begin("LoadModXml", 1000f);

        Expect.AreApproximatelyEqual(0f, ledger.Entries[1].WallMs);
    }

    [Test]
    public static void AStageNeverEndsBeforeItBegan()
    {
        var ledger = new StageLedger("Initializing");
        ledger.Begin("LoadModXml", 1000f);
        ledger.Close(900f);

        Expect.AreApproximatelyEqual(0f, ledger.Entries[1].WallMs);
    }
}
