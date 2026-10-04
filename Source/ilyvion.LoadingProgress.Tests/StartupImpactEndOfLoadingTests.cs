using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

// The four stretches at the end of loading that used to have no owner, checked in this
// startup's own session.
[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactEndOfLoadingTests
{
    private const int MaxFramesToWaitForTheClock = 1200;

    [Test]
    public static IEnumerator TheEndOfLoadingIsRecordedUnderItsOwners()
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        if (!startupImpact.WasTrackingEnabledAtStartup)
        {
            Test.Skip("Startup impact tracking was off for this launch.");
            yield break;
        }

        // A command-line test run can start before the clock stops.
        var framesWaited = 0;
        while (!startupImpact.LoadingTimeMeasured && framesWaited++ < MaxFramesToWaitForTheClock)
        {
            yield return null;
        }

        var baseGame = startupImpact.BaseGameProfiler.Metrics;
        Expect.Any(
            baseGame.Keys,
            key =>
                key == StaticConstructorOnStartupUtilityReplacement.CallAllPassCategory
                || key.StartsWith(
                    StaticConstructorOnStartupUtilityReplacement.CallAllPassWithUntimedHooksKey
                        + "|",
                    StringComparison.Ordinal
                )
        );
        Expect.IsTrue(
            baseGame.TryGetValue(
                StaticConstructorOnStartupUtilityReplacement.GarbageCollectionCategory,
                out var collection
            )
        );
        Expect.GreaterThan(collection, 0f);
        if (Prefs.DevMode)
        {
            Expect.IsTrue(
                baseGame.ContainsKey(
                    StaticConstructorOnStartupUtilityReplacement.ReportMissingAttributesCategory
                )
            );
        }

        var own = startupImpact.Modlist.GetModInfoFor(LoadingProgressMod.instance.Content);
        Expect.IsNotNull(own);
        Expect.IsTrue(
            own!.Profiler.Metrics.TryGetValue(
                "LoadingProgress.StartupImpact.ModConstructor",
                out var constructor
            )
        );
        Expect.GreaterThan(constructor, 0f);
    }

    // The patches that time the hooks come off with the call, whether tracking was on or not.
    [Test]
    public static void NoTimingPatchIsLeftOnTheStaticConstructorCall()
    {
        var onCallAll = Harmony.GetPatchInfo(
            AccessTools.Method(
                typeof(StaticConstructorOnStartupUtility),
                nameof(StaticConstructorOnStartupUtility.CallAll)
            )
        );

        Expect.IsTrue(onCallAll == null || !onCallAll.Owners.Contains(CallAllHookTiming.HarmonyId));
    }
}
