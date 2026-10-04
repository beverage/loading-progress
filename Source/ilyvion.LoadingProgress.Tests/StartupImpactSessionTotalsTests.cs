using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionTotalsTests
{
    private const string ClearCache =
        "LoadingProgress.StartupImpact.AbstractFilesystemClearAllCache";
    private const string PostLoadEvent =
        "LoadingProgress.StartupImpact.PostLoadLongEvent|Initializing interface";

    // Loading stops the clock at 90.6 s and the menu is usable at 110.6 s. The base game has
    // 14.1 s of loading and a 5.9 s long event after it; one mod has 50 s.
    private static StartupImpactSessionData Session(float timeToMenu) =>
        StartupImpactSessionData.FromValues(
            90600f,
            timeToMenu,
            new() { [ClearCache] = 14100f, [PostLoadEvent] = 5900f },
            [
                StartupImpactSessionModData.FromValues(
                    "A",
                    "test.a",
                    new() { ["LoadingProgress.StartupImpact.ModConstructor"] = 50000f },
                    []
                ),
            ],
            [new("LoadingDefs", 1500f, 500f)]
        );

    [Test]
    public static void TheTopBarSpansTheTimeToTheMenu()
    {
        var session = Session(110600f);
        var viewData = new StartupImpactSessionViewData(session);

        Expect.AreApproximatelyEqual(110600f, viewData.TotalWindow);
        // What the mod and the base game do not account for, measured to the menu.
        Expect.AreApproximatelyEqual(110600f - 50000f - 20000f, viewData.MetricsTotal[3]);
        // The stored loading time is the clock stop, which external tools read; it is left
        // as it was.
        Expect.AreApproximatelyEqual(90600f, session.LoadingTime);
    }

    [Test]
    public static void WithoutATimeToTheMenuTheTopBarSpansTheLoadingTime() =>
        Expect.AreApproximatelyEqual(
            90600f,
            new StartupImpactSessionViewData(Session(0f)).TotalWindow
        );

    // The 5.9 s event after loading has an owner; only the rest of the 20 s is remaining.
    [Test]
    public static void TheTimeAfterLoadingLeavesOutTheTimedEvents()
    {
        var viewData = new StartupImpactSessionViewData(Session(110600f));

        var afterLoading = viewData.RemainingByStage.Single(entry =>
            entry.Key == StartupImpactSessionViewData.AfterLoadingKey
        );
        Expect.AreApproximatelyEqual(14100f, afterLoading.Ms);
    }

    [Test]
    public static void ASessionSavedAndReadBackKeepsItsStagesAndTimeToTheMenu()
    {
        var loaded = SaveAndLoad(Session(110600f));

        Expect.IsNotNull(loaded);
        Expect.AreApproximatelyEqual(110600f, loaded!.TimeToMenu);
        Expect.AreEqual(1, loaded.StageTimings.Count);
        Expect.AreEqual("LoadingDefs", loaded.StageTimings[0].Stage);
        Expect.AreApproximatelyEqual(1500f, loaded.StageTimings[0].WallMs);
        Expect.AreApproximatelyEqual(500f, loaded.StageTimings[0].AttributedMs);
    }

    // A session file written by 0.17.0 has neither node. It reads as a session with no stages
    // and no time to the menu, not as one that throws.
    [Test]
    public static void AFileFromBeforeStagesWereKeptReadsAsNoStages()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lp-session-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(
                path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                    + "<StartupImpactSession><sessionData>"
                    + "<loadingTime>90600</loadingTime>"
                    + $"<metrics><keys><li>{ClearCache}</li></keys><values><li>14100</li></values></metrics>"
                    + "<totalImpact>14100</totalImpact>"
                    + "<offThreadMetrics><keys /><values /></offThreadMetrics>"
                    + "<offThreadTotalImpact>0</offThreadTotalImpact>"
                    + "<mods />"
                    + "</sessionData></StartupImpactSession>"
            );

            var loaded = Load(path);

            Expect.IsNotNull(loaded);
            Expect.IsNotNull(loaded!.StageTimings);
            Expect.IsEmpty(loaded.StageTimings);
            Expect.AreApproximatelyEqual(0f, loaded.TimeToMenu);
            Expect.IsEmpty(new StartupImpactSessionViewData(loaded).RemainingByStage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static StartupImpactSessionData? SaveAndLoad(StartupImpactSessionData session)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lp-session-{Guid.NewGuid():N}.xml");
        try
        {
            Scribe.saver.InitSaving(path, "StartupImpactSession");
            Scribe_Deep.Look(ref session, "sessionData");
            Scribe.saver.FinalizeSaving();
            return Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static StartupImpactSessionData? Load(string path)
    {
        StartupImpactSessionData? loaded = null;
        Scribe.loader.InitLoading(path);
        Scribe_Deep.Look(ref loaded, "sessionData");
        Scribe.loader.FinalizeLoading();
        return loaded;
    }
}
