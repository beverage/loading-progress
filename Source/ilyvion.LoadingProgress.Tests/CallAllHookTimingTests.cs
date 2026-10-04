using System.Diagnostics;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class CallAllHookTimingTests
{
    private const string TestHarmonyId = "ilyvion.LoadingProgress.Tests.CallAllHookTiming";
    private const string TestBaseCategory = "LoadingProgress.Tests.CallAllHookTiming.Call";
    private const string HookCategoryPrefix =
        $"{CallAllHookTiming.Category}|{nameof(CallAllHookTimingTests)}.";
    private const string TrackingOff = "Startup impact tracking is off.";

    // The method under test, with a body for Harmony to patch. Not inlined, so the call from
    // the helper below still reaches the patched method.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Target() => _ = Stopwatch.GetTimestamp();

    // Spins for a few milliseconds, as a hook with real work would.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SlowPostfix()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5) { }
    }

    // A few bytes of IL around the real work, the shape of many mods' hooks, and small enough
    // for the runtime to inline into the patched method's replacement.
    public static void ThinPostfix() => SlowPostfix();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowingPostfix() =>
        throw new InvalidOperationException("A hook that fails, for the test.");

    private static MethodInfo TargetMethod =>
        AccessTools.Method(typeof(CallAllHookTimingTests), nameof(Target));

    private static MethodInfo HookMethod =>
        AccessTools.Method(typeof(CallAllHookTimingTests), nameof(SlowPostfix));

    private static Profiler BaseGame => LoadingProgressMod.instance.StartupImpact.BaseGameProfiler;

    // One heading naming every mod with a hook on the call cannot be hidden with the mod it
    // belongs to; a hook timed under its own mod can.
    [Test]
    public static void AHookIsTimedUnderTheModThatOwnsIt()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            return;
        }

        Expect.GreaterThanOrEqualTo(TimedUnderOwnMod(nameof(SlowPostfix)), 4f);
    }

    // The replacement Harmony compiles for a patched method takes a copy of a hook this small
    // instead of a call to it, and a detour put on the hook afterwards is never reached from
    // there unless the replacement is built again once the detour is in place.
    [Test]
    public static void AHookSmallEnoughToBeInlinedIsStillTimed()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            return;
        }

        Expect.GreaterThanOrEqualTo(TimedUnderOwnMod(nameof(ThinPostfix)), 4f);
    }

    // The call's own category stops while a hook runs, or every hook's time would be counted
    // twice: on its mod, and again under the call.
    [Test]
    public static void TheCallsCategoryPausesWhileAHookRuns()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            return;
        }

        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(SlowPostfix))
        );
        try
        {
            TimedCall();

            Expect.IsTrue(BaseGame.Metrics.TryGetValue(TestBaseCategory, out var underCall));
            Expect.LessThanOrEqualTo(underCall, 3f);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    // A hook that throws still has its timing closed and the call's category restarted, so
    // the call's category stops cleanly; stopping one that is not running logs an error,
    // which fails the test.
    [Test]
    public static void AHookThatThrowsLeavesTheTimingIntact()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            return;
        }

        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(ThrowingPostfix))
        );
        try
        {
            var threw = false;
            TimedCall(() =>
            {
                try
                {
                    Target();
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }
            });

            Expect.IsTrue(threw);
            Expect.IsTrue(BaseGame.Metrics.ContainsKey(TestBaseCategory));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    [Test]
    public static void TheTimingPatchesComeOffAgainWithTheCall()
    {
        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(SlowPostfix))
        );
        try
        {
            var timing = CallAllHookTiming.Install(TargetMethod);
            Expect.IsNotNull(Harmony.GetPatchInfo(HookMethod));
            Expect.IsTrue(
                Harmony.GetPatchInfo(TargetMethod).Owners.Contains(CallAllHookTiming.HarmonyId)
            );

            timing.Remove();

            var onHook = Harmony.GetPatchInfo(HookMethod);
            Expect.IsTrue(onHook == null || onHook.Owners.Count == 0);
            var onTarget = Harmony.GetPatchInfo(TargetMethod);
            Expect.IsFalse(onTarget.Owners.Contains(CallAllHookTiming.HarmonyId));
            Expect.IsTrue(onTarget.Owners.Contains(harmony.Id));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // The usual case: every hook is timed under its own mod, and the call names no one.
    [Test]
    public static void ThePassNamesNoOneWhenEveryHookIsTimed()
    {
        var category = StaticConstructorOnStartupUtilityReplacement.CallAllPassCategoryFor([]);

        Expect.IsFalse(category.Contains('|', StringComparison.Ordinal));
        Expect.IsTrue(category.CanTranslate());
    }

    [Test]
    public static void ThePassNamesTheModsWhoseHooksCouldNotBeTimed()
    {
        var text = StartupImpactProfilerUtil.TranslateCategory(
            StaticConstructorOnStartupUtilityReplacement.CallAllPassCategoryFor(["Mod A", "Mod B"])
        );

        Expect.IsTrue(text.Contains("Mod A, Mod B", StringComparison.Ordinal));
        Expect.IsFalse(text.Contains("{0}", StringComparison.Ordinal));
    }

    // Times one call of the target under the test's own base-game category, the way the
    // startup times the engine's pass, with the timing patches on for the call only.
    private static void TimedCall(Action? call = null)
    {
        var timing = CallAllHookTiming.Install(TargetMethod);
        CallAllHookTiming.BaseCategory = TestBaseCategory;
        StartupImpactProfilerUtil.StartBaseGameProfiler(TestBaseCategory);
        try
        {
            (call ?? Target)();
        }
        finally
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler(TestBaseCategory);
            timing.Remove();
        }
    }

    // Patches the target with the named hook of this class, times one call through the timing
    // patches and returns the milliseconds credited to this assembly's mod under the hook's
    // category.
    private static float TimedUnderOwnMod(string hook)
    {
        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), hook)
        );
        try
        {
            var info = OwnModInfo();
            Expect.IsNotNull(info);
            var category = $"{HookCategoryPrefix}{hook}";
            _ = info!.Profiler.Metrics.TryGetValue(category, out var before);

            var timing = CallAllHookTiming.Install(TargetMethod);
            Expect.IsEmpty(timing.UntimedOwners);
            Target();
            timing.Remove();

            Expect.IsTrue(info.Profiler.Metrics.TryGetValue(category, out var after));
            return after - before;
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    private static ModInfo? OwnModInfo() =>
        Utilities.FindModByAssembly(typeof(CallAllHookTimingTests).Assembly) is { } mod
            ? LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod)
            : null;

    // Takes everything these tests timed back out of the live session, totals included: the
    // test's own base-game category, and its hooks' categories on this assembly's mod. The
    // session is saved when the menu is reached, and a test run starts before that.
    private static void ForgetTestTime()
    {
        Forget(BaseGame, TestBaseCategory);
        if (OwnModInfo() is not { } info)
        {
            return;
        }
        foreach (
            var category in info
                .Profiler.Metrics.Keys.Where(key =>
                    key.StartsWith(HookCategoryPrefix, StringComparison.Ordinal)
                )
                .ToList()
        )
        {
            Forget(info.Profiler, category);
        }
    }

    private static void Forget(Profiler profiler, string category)
    {
        if (profiler.Metrics.TryGetValue(category, out var ms))
        {
            profiler.Discount(category, ms);
            _ = profiler.Metrics.TryRemove(category, out _);
        }
    }
}
