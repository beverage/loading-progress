namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Times other mods' Harmony patches on one method, each under the mod that owns it, for the
/// duration of one call.
/// </summary>
/// <remarks>
/// The engine's <c>StaticConstructorOnStartupUtility.CallAll</c> is run a second time after
/// Loading Progress has run every constructor itself, purely so other mods' prefixes and
/// postfixes on it still fire. Every constructor is a no-op by then, so what that call costs
/// is the hooks, and one base-game heading naming their owners cannot be hidden along with the
/// mod a hook belongs to. Patching the patch methods themselves, with a prefix and a
/// finalizer, puts each hook's time on its own mod under its own category, and the patches
/// come off again as soon as the call returns. The base-game category covering the call is
/// paused while a hook runs, so no millisecond is counted twice.
/// </remarks>
internal sealed class CallAllHookTiming
{
    internal const string Category =
        "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAllHook";
    internal const string HarmonyId = "ilyvion.LoadingProgress.CallAllHookTiming";

    private static readonly Dictionary<MethodBase, Timed> _timed = [];
    private static int _depth;

    private readonly Harmony _harmony = new(HarmonyId);
    private readonly List<MethodBase> _patched = [];
    private readonly List<string> _untimedOwners = [];
    private MethodBase? _rebuilt;

    private sealed record Timed(ModContentPack Mod, string Category);

    private CallAllHookTiming() { }

    /// <summary>
    /// The mods whose hooks could not be timed on their own, by name, sorted. Their time stays
    /// under the base-game category for the call.
    /// </summary>
    internal IReadOnlyList<string> UntimedOwners => _untimedOwners.AsReadOnly();

    /// <summary>
    /// The base-game category the call runs under, paused while a timed hook runs.
    /// </summary>
    internal static string? BaseCategory { get; set; }

    /// <summary>
    /// Patches every prefix, postfix and finalizer other mods have on <paramref name="target"/>
    /// so each is timed under its owner. Transpilers are not hooks and are left alone.
    /// </summary>
    internal static CallAllHookTiming Install(MethodBase target)
    {
        var timing = new CallAllHookTiming();
        _depth = 0;

        var patches = Harmony.GetPatchInfo(target);
        if (patches == null)
        {
            return timing;
        }

        foreach (var patch in patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Finalizers))
        {
            var method = patch.PatchMethod;
            var assembly = method?.DeclaringType?.Assembly;
            if (method == null || assembly == null || assembly == Assembly.GetExecutingAssembly())
            {
                continue;
            }

            var mod = Utilities.FindModByAssembly(assembly);
            var ownerName = mod?.Name ?? patch.owner;
            if (mod == null || _timed.ContainsKey(method))
            {
                timing.AddUntimed(ownerName);
                continue;
            }

            try
            {
                _timed[method] = new Timed(
                    mod,
                    $"{Category}|{method.DeclaringType.Name}.{method.Name}"
                );
                _ = timing._harmony.Patch(
                    method,
                    prefix: new HarmonyMethod(typeof(CallAllHookTiming), nameof(Prefix)),
                    finalizer: new HarmonyMethod(typeof(CallAllHookTiming), nameof(Finalizer))
                );
                timing._patched.Add(method);
            }
            catch (Exception e)
            {
                _ = _timed.Remove(method);
                timing.AddUntimed(ownerName);
                LoadingProgressMod.Warning(
                    $"Could not time {ownerName}'s hook {method.DeclaringType?.Name}.{method.Name} on its own: {e.Message}"
                );
            }
        }

        if (timing._patched.Count > 0)
        {
            timing.RebuildReplacementOf(target);
        }
        timing._untimedOwners.Sort(StringComparer.Ordinal);
        return timing;
    }

    /// <summary>
    /// Has Harmony emit and compile <paramref name="target"/>'s replacement again, now that
    /// its hooks carry their timing patches.
    /// </summary>
    /// <remarks>
    /// The replacement was compiled when the last mod patched the target, and Mono's JIT
    /// inlines a hook of under about twenty bytes of IL into it then, so the detour a timing
    /// patch puts on such a hook is never reached from there: a thin wrapper around a mod's
    /// real initialization would stay on the base-game heading unnoticed. Patching a hook
    /// marks it as not to be inlined from then on, and one more patch on the target makes
    /// Harmony build the replacement afresh, so it calls the hook through its detour. The
    /// patch does nothing itself and comes off with the rest.
    /// </remarks>
    private void RebuildReplacementOf(MethodBase target)
    {
        try
        {
            _ = _harmony.Patch(
                target,
                prefix: new HarmonyMethod(typeof(CallAllHookTiming), nameof(NoOpPrefix))
            );
            _rebuilt = target;
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning(
                $"Could not rebuild {target.DeclaringType?.Name}.{target.Name} for hook timing: {e.Message}"
            );
        }
    }

    /// <summary>
    /// Takes the timing patches off again, leaving the hooks as they were.
    /// </summary>
    internal void Remove()
    {
        foreach (var method in _patched)
        {
            try
            {
                _harmony.Unpatch(method, HarmonyPatchType.All, _harmony.Id);
            }
            catch (Exception e)
            {
                LoadingProgressMod.Warning(
                    $"Could not remove the timing patch from {method.DeclaringType?.Name}.{method.Name}: {e.Message}"
                );
            }
            _ = _timed.Remove(method);
        }
        _patched.Clear();
        if (_rebuilt is { } target)
        {
            try
            {
                _harmony.Unpatch(target, HarmonyPatchType.Prefix, _harmony.Id);
            }
            catch (Exception e)
            {
                LoadingProgressMod.Warning(
                    $"Could not remove the rebuild patch from {target.DeclaringType?.Name}.{target.Name}: {e.Message}"
                );
            }
            _rebuilt = null;
        }
        BaseCategory = null;
        _depth = 0;
    }

    private void AddUntimed(string owner)
    {
        if (!_untimedOwners.Contains(owner))
        {
            _untimedOwners.Add(owner);
        }
    }

    // Does nothing: patching the target with it is what rebuilds the target's replacement.
    private static void NoOpPrefix() { }

    private static void Prefix(MethodBase __originalMethod)
    {
        if (!_timed.TryGetValue(__originalMethod, out var timed))
        {
            return;
        }

        if (_depth++ == 0 && BaseCategory is { } paused)
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler(paused);
        }
        StartupImpactProfilerUtil.StartModProfiler(timed.Mod, timed.Category);
    }

    private static Exception? Finalizer(Exception? __exception, MethodBase __originalMethod)
    {
        if (_timed.TryGetValue(__originalMethod, out var timed))
        {
            StartupImpactProfilerUtil.StopModProfiler(timed.Mod, timed.Category);
            if (--_depth == 0 && BaseCategory is { } paused)
            {
                StartupImpactProfilerUtil.StartBaseGameProfiler(paused);
            }
        }
        return __exception;
    }
}
