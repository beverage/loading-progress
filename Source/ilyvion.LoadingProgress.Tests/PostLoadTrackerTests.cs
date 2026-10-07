using System.Text;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

// Some of these tests wait through the startup's tail, while other mods' post-load events run.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class PostLoadTrackerTests
{
    private const string TrackingOff = "Startup impact tracking is off.";

    [Test]
    public static void TheFirstIdleFrameNeverSettlesTheMenu() =>
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(-1f, 100000f, 1));

    [Test]
    public static void TwoCloseIdleFramesSettleTheMenu() =>
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(100000f, 100016f, 2));

    // A mod building its state on the menu's first frame stalls the main thread for seconds
    // with nothing queued; the frame after that stall must not count, or the stall is left
    // out of the time to the menu.
    [Test]
    public static void AnIdleFrameAfterAStallDoesNotSettleTheMenu() =>
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(100000f, 107700f, 2));

    [Test]
    public static void TheFrameAfterTheStalledOneSettlesTheMenu() =>
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(107700f, 107716f, 3));

    [Test]
    public static void FramesAQuarterOfASecondApartAreNotClose()
    {
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(1000f, 1249.9f, 2));
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(1000f, 1250f, 2));
    }

    // A menu that never draws two frames close together, below four frames a second, would
    // otherwise keep the startup open until the player left the menu.
    [Test]
    public static void ASlowMenuSettlesAfterFiveIdleFrames()
    {
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(1000f, 1400f, 4));
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(1000f, 1400f, 5));
    }

    // A player who switched to another window while the game loaded: the game stopped between
    // two frames until they came back, and that wait is theirs, not the startup's.
    [Test]
    public static void AWaitInTheBackgroundIsAPause() =>
        Expect.AreApproximatelyEqual(
            600000f,
            PostLoadTracker.PauseIn(1000f, 601000f, unfocusedSinceFrameEnd: true, false)
        );

    [Test]
    public static void AWaitWithTheGameInFrontIsNoPause() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(1000f, 601000f, unfocusedSinceFrameEnd: false, false)
        );

    // With 'Run in background' on the game never stops, so a long wait is something else.
    [Test]
    public static void AGameThatRunsInTheBackgroundIsNeverPaused() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(1000f, 601000f, unfocusedSinceFrameEnd: true, true)
        );

    [Test]
    public static void AnOrdinaryFrameIsNoPause() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(1000f, 1016f, unfocusedSinceFrameEnd: true, false)
        );

    [Test]
    public static void NothingIsPausedBeforeAFrameHasEnded() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(-1f, 601000f, unfocusedSinceFrameEnd: true, false)
        );

    // A synchronous long event runs inside LongEventsUpdate, before the tracker looks in its
    // postfix. The pause used to be measured up to that look, so an event that ran for seconds
    // in the frame after a pause was taken off with it; it ends where the frame's long events
    // begin.
    [Test]
    public static void AnEventInTheFrameAfterAPauseIsNotPartOfIt() =>
        Expect.AreApproximatelyEqual(
            60000f,
            PostLoadTracker.PauseBeforeThisFrame(
                1000f,
                61000f,
                64000f,
                unfocusedSinceFrameEnd: true,
                false
            )
        );

    [Test]
    public static void WithNoFrameStartThePauseRunsToNow() =>
        Expect.AreApproximatelyEqual(
            63000f,
            PostLoadTracker.PauseBeforeThisFrame(
                1000f,
                -1f,
                64000f,
                unfocusedSinceFrameEnd: true,
                false
            )
        );

    // The frame start comes from the tracker's prefix on LongEventsUpdate.
    [Test]
    public static void TheFrameStartIsRecordedBeforeTheFramesLongEvents()
    {
        var patches = Harmony.GetPatchInfo(
            AccessTools.Method(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsUpdate))
        );

        Expect.IsNotNull(patches);
        Expect.IsTrue(
            patches.Prefixes.Any(patch =>
                patch.PatchMethod.DeclaringType == typeof(LongEventHandler_LongEventsUpdate_Patches)
            )
        );
    }

    // With the initialization patches off in the settings, nothing used to make the main
    // thread the active one after loading, so a long event timed there went to the off-thread
    // figures, which the totals never see and a pause cannot be taken off.
    [Test]
    public static IEnumerator AnEventTimedAfterLoadingCountsOnTheMainThread()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            yield break;
        }

        var framesWaited = 0;
        while (TestStartup.StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        var category = $"{PostLoadTracker.Category}|{nameof(PostLoadTrackerTests)}.Event";
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        var profiler = startupImpact.BaseGameProfiler;

        // As though the loading thread were still the active one.
        Task.Run(startupImpact.UpdateActiveThreadId).Wait();
        try
        {
            Expect.IsFalse(startupImpact.IsActiveThread());

            PostLoadTracker.StartTiming(null, isBaseGame: true, category);
            StartupImpactProfilerUtil.StopBaseGameProfiler(category);

            Expect.IsTrue(startupImpact.IsActiveThread());
            Expect.IsTrue(profiler.Metrics.ContainsKey(category));
        }
        finally
        {
            startupImpact.UpdateActiveThreadId();
            Forget(profiler, category);
        }
    }

    // The same for Loading Progress's own work after loading, such as saving its report.
    [Test]
    public static IEnumerator WorkTimedAfterLoadingCountsOnTheMainThread()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            yield break;
        }

        var framesWaited = 0;
        while (TestStartup.StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        const string description = nameof(PostLoadTrackerTests);
        var category = $"{PostLoadTracker.Category}|{description}";
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        var info = startupImpact.Modlist.GetModInfoFor(LoadingProgressMod.instance.Content);
        Expect.IsNotNull(info);
        var profiler = info!.Profiler;

        // As though the loading thread were still the active one.
        Task.Run(startupImpact.UpdateActiveThreadId).Wait();
        try
        {
            Expect.IsFalse(startupImpact.IsActiveThread());

            PostLoadTracker.RunAsOwnWork(description, () => { });

            Expect.IsTrue(startupImpact.IsActiveThread());
            Expect.IsTrue(profiler.Metrics.ContainsKey(category));
        }
        finally
        {
            startupImpact.UpdateActiveThreadId();
            Forget(profiler, category);
        }
    }

    // An event that was current through a pause is stopped with the pause taken back off, on
    // the timer it was started on. A pause longer than the event leaves it nothing.
    [Test]
    public static IEnumerator APauseIsTakenOffTheTimerTheEventRanOn()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            yield break;
        }

        var framesWaited = 0;
        while (TestStartup.StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        var category = $"{PostLoadTracker.Category}|{nameof(PostLoadTrackerTests)}.Paused";
        var profiler = LoadingProgressMod.instance.StartupImpact.BaseGameProfiler;
        try
        {
            PostLoadTracker.StartTiming(null, isBaseGame: true, category);
            StartupImpactProfilerUtil.Stop(null, isBaseGame: true, category, discountMs: 60000f);

            Expect.IsTrue(profiler.Metrics.TryGetValue(category, out var ms));
            Expect.AreApproximatelyEqual(0f, ms);
        }
        finally
        {
            Forget(profiler, category);
        }
    }

    // Takes a test's category back out of the live session, totals included. The test waited
    // for the startup to complete, so the stage ledger, closed by then, holds none of it.
    private static void Forget(Profiler profiler, string category)
    {
        if (profiler.Metrics.TryGetValue(category, out var ms))
        {
            profiler.Discount(category, ms);
            _ = profiler.Metrics.TryRemove(category, out _);
        }
        _ = profiler.OffThreadMetrics.TryRemove(category, out _);
    }

    [Test]
    public static void AnEventIsNamedByItsTextWhenTheKeyTranslates() =>
        Expect.AreEqual(
            "LoadingProgress.Title".Translate().ToString(),
            PostLoadTracker.Describe("LoadingProgress.Title", null, null)
        );

    [Test]
    public static void AKeyThatDoesNotTranslateIsShownAsItIs() =>
        Expect.AreEqual(
            "NoSuchKey.ForThisTest",
            PostLoadTracker.Describe("NoSuchKey.ForThisTest", null, null)
        );

    // An event with no text used to be named after the compiler's closure class and method,
    // which tell a player nothing.
    [Test]
    public static void ALambdaIsNamedAfterTheMethodItWasWrittenIn()
    {
        Action action = static () => { };

        Expect.AreEqual(
            $"{typeof(PostLoadTrackerTests).FullName}.{nameof(ALambdaIsNamedAfterTheMethodItWasWrittenIn)}",
            PostLoadTracker.Describe(null, action, null)
        );
    }

    [Test]
    public static void AnIteratorIsNamedAfterItsMethod() =>
        Expect.AreEqual(
            $"{typeof(PostLoadTrackerTests).FullName}.{nameof(Steps)}",
            PostLoadTracker.Describe(null, null, Steps())
        );

    [Test]
    public static void AnEventIsOwnedByTheModWhoseCodeItRuns()
    {
        Action action = static () => { };

        var owner = PostLoadTracker.OwnerOf(action, null, out var isBaseGame);

        Expect.IsNotNull(owner);
        Expect.ReferencesAreEqual(
            Utilities.FindModByAssembly(typeof(PostLoadTrackerTests).Assembly),
            owner
        );
        Expect.IsFalse(isBaseGame);
    }

    [Test]
    public static void TheEnginesOwnEventBelongsToTheBaseGame()
    {
        Action action = LongEventHandler.ClearQueuedEvents;

        var owner = PostLoadTracker.OwnerOf(action, null, out var isBaseGame);

        Expect.IsNull(owner);
        Expect.IsTrue(isBaseGame);
    }

    // An event from code no mod loaded used to go under the base game, while a deferred
    // action from the same code went untimed. Both now follow the deferred actions' rule.
    [Test]
    public static void AnEventFromCodeNoModLoadedBelongsToNeither()
    {
        var holder = new StringBuilder();
        Func<string> action = holder.ToString;
        IEnumerator enumerator = new List<int>().GetEnumerator();

        var actionOwner = PostLoadTracker.OwnerOf(action, null, out var actionIsBaseGame);
        var enumeratorOwner = PostLoadTracker.OwnerOf(
            null,
            enumerator,
            out var enumeratorIsBaseGame
        );

        Expect.IsNull(actionOwner);
        Expect.IsFalse(actionIsBaseGame);
        Expect.IsNull(enumeratorOwner);
        Expect.IsFalse(enumeratorIsBaseGame);
    }

    private static IEnumerator Steps()
    {
        yield break;
    }
}
