using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

// Some of these tests wait through the startup's tail, while other mods' post-load events run.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class DeferredActionRunTests
{
    private const string Label = "ilyvion.LoadingProgress.Tests.DeferredActionRunTests -> Test";
    private const string Category =
        $"{LongEventHandler_ExecuteToExecuteWhenFinished_Patches.DeferredActionCategory}|{Label}";
    private const string TrackingOff = "Startup impact tracking is off.";

    // An action that threw used to leave its category open on its owner's timer: the time it
    // had run was never recorded, and every category started there afterwards ran inside it
    // for the rest of the startup.
    [Test]
    [ErrorsAllowed("Could not execute post-long-event action")]
    public static IEnumerator AnActionThatThrowsHasItsCategoryClosed()
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

        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);
        try
        {
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
                () =>
                    throw new InvalidOperationException(
                        "A deferred action that fails, for the test."
                    ),
                Label
            );

            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(Category));
        }
        finally
        {
            // If the category was left open, close it, or every later category on this timer
            // would run inside it for the rest of the session.
            if (!info!.Profiler.Metrics.ContainsKey(Category))
            {
                StartupImpactProfilerUtil.StopModProfiler(mod, Category);
            }
            Forget(info.Profiler, Category);
        }
    }

    [Test]
    public static IEnumerator AnActionRunsTimedUnderItsOwner()
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

        var info = OwnModInfo();
        Expect.IsNotNull(info);
        try
        {
            var ran = false;
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
                () => ran = true,
                Label
            );

            Expect.IsTrue(ran);
            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(Category));
        }
        finally
        {
            Forget(info!.Profiler, Category);
        }
    }

    // A failure in the timing used to skip the action itself, and the error blamed the action.
    [Test]
    [WarningsAllowed("Could not time the deferred action")]
    public static void AnActionWhoseTimingFailsStillRuns()
    {
        var ran = false;
        LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
            () => ran = true,
            Label,
            _ => throw new InvalidOperationException("An owner lookup that fails, for the test.")
        );

        Expect.IsTrue(ran);
    }

    private static ModContentPack? OwnMod() =>
        Utilities.FindModByAssembly(typeof(DeferredActionRunTests).Assembly);

    private static ModInfo? OwnModInfo() =>
        OwnMod() is { } mod
            ? LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod)
            : null;

    // Takes the test's category back out of the live session, totals included. The timed
    // tests wait for the startup to complete, so the stage ledger, closed by then, holds none
    // of it.
    private static void Forget(Profiler profiler, string category)
    {
        if (profiler.Metrics.TryGetValue(category, out var ms))
        {
            profiler.Discount(category, ms);
            _ = profiler.Metrics.TryRemove(category, out _);
        }
    }
}
