using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionIndexEntryTests
{
    // Regression. LoadingTime comes from ProfilerStopwatch, which returns milliseconds, but
    // the neighbouring Settings.LoadingTimes holds seconds. Reading this one as seconds showed
    // a real 10 second load as 2:48:27.
    [Test]
    public static void LoadingTimeIsReadAsMillisecondsRatherThanSeconds() =>
        Expect.AreEqual("00:10", StartupImpactSessionIndexEntry.FormatLoadingTime(10107.63f));

    [Test]
    public static void AnEmptyStageTranslatesToNothing() =>
        Expect.AreEqual("", StartupImpactSessionIndexEntry.TranslateStage(""));

    [Test]
    public static void AStageWithNoTextOfItsOwnComesBackAsItWasRecorded() =>
        Expect.AreEqual(
            "NoSuchStage",
            StartupImpactSessionIndexEntry.TranslateStage("NoSuchStage")
        );

    // The stage strings carry the mod being worked on as {0} and end in an ellipsis, and
    // neither belongs inside "Stopped at ...". Asserted without naming the English text, so
    // the suite does not depend on which language is active.
    [Test]
    public static void AStageLosesItsModPlaceholderAndItsEllipsis()
    {
        var text = StartupImpactSessionIndexEntry.TranslateStage("LoadModXml");

        Expect.AreEqual(-1, text.IndexOf("{0}", StringComparison.Ordinal));
        Expect.IsFalse(text.EndsWith('.'));
        Expect.AreNotEqual("LoadModXml", text);
    }

    private static StartupImpactSessionData Session(float timeToMenu) =>
        StartupImpactSessionData.FromValues(
            90600f,
            timeToMenu,
            new() { ["LoadingProgress.StartupImpact.AbstractFilesystemClearAllCache"] = 1000f },
            [],
            []
        );

    // The picker lists a session by the same figure the startup impact window titles it with:
    // the time to the main menu once the session has one.
    [Test]
    public static void AnEntryListsTheTimeToTheMenuWhenTheSessionHasOne()
    {
        var entry = StartupImpactSessionIndexEntry.ForCompletedSession("to-menu", Session(110600f));

        Expect.AreApproximatelyEqual(110600f, entry.LoadingTime);
        Expect.IsTrue(entry.MeasuredToMenu);
    }

    // A session from before the time to the menu was measured is listed by the point the clock
    // stopped, and marked, since it reads shorter than a newer session of the same mods.
    [Test]
    public static void AnEntryWithoutATimeToTheMenuIsListedByTheLoadingTime()
    {
        var entry = StartupImpactSessionIndexEntry.ForCompletedSession("to-end", Session(0f));

        Expect.AreApproximatelyEqual(90600f, entry.LoadingTime);
        Expect.IsFalse(entry.MeasuredToMenu);
    }

    // The session is first recorded when loading finishes and recorded again at the main menu,
    // under the same entry, which then takes the time to the menu.
    [Test]
    public static void SavingTheSessionAgainAtTheMenuUpdatesItsEntry()
    {
        var entry = StartupImpactSessionIndexEntry.ForCompletedSession("again", Session(0f));

        entry.UpdateFrom(Session(110600f));

        Expect.AreApproximatelyEqual(110600f, entry.LoadingTime);
        Expect.IsTrue(entry.MeasuredToMenu);
    }

    // The second delayed-initialization pass has no string of its own. It used to be listed
    // by its raw member name, in every language.
    [Test]
    public static void TheSecondDelayedInitialisationPassBorrowsTheFirstOnesText()
    {
        var second = StartupImpactSessionIndexEntry.TranslateStage(
            nameof(LoadingStage.ExecuteToExecuteWhenFinished2)
        );

        Expect.AreEqual(
            StartupImpactSessionIndexEntry.TranslateStage(
                nameof(LoadingStage.ExecuteToExecuteWhenFinished)
            ),
            second
        );
        Expect.AreNotEqual(nameof(LoadingStage.ExecuteToExecuteWhenFinished2), second);
    }
}
