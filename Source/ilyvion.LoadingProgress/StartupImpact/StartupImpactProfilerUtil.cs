namespace ilyvion.LoadingProgress.StartupImpact;

internal static class StartupImpactProfilerUtil
{
    /// <summary>
    /// The mod a deferred initialization action is credited to: the mod whose def it sets up
    /// when it can be traced to one, else the mod whose assembly it runs from. Null with
    /// <paramref name="isBaseGame"/> true for the engine's own actions that trace to no def;
    /// null with it false for an action from an assembly no mod owns.
    /// </summary>
    /// <remarks>
    /// The engine queues one such action per def for graphics and references, every one from
    /// its own assembly, so the def is the better guide: a framework's per-def work belongs to
    /// the def's mod as well.
    /// </remarks>
    public static ModContentPack? OwnerOfDeferredAction(Delegate action, out bool isBaseGame)
    {
        var assembly = action.Method.DeclaringType?.Assembly;
        var owner =
            DeferredActionOwner.OwningContentPack(action)
            ?? (assembly == null ? null : Utilities.FindModByAssembly(assembly));
        isBaseGame =
            owner == null
            && assembly != null
            && assembly.FullName.StartsWith("Assembly-CSharp", StringComparison.Ordinal);
        return owner;
    }

    public static void StartModProfiler(ModContentPack? mod, string key)
    {
        if (mod == null)
        {
            return;
        }

        var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod);
        info?.Start(key);
    }

    public static void StopModProfiler(ModContentPack? mod, string key)
    {
        if (mod == null)
        {
            return;
        }

        var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod);
        _ = info?.Stop(key);
    }

    public static void StartBaseGameProfiler(string key) =>
        // LoadingProgressMod.DevMessage($"Starting base game profiler for {key}");
        LoadingProgressMod.instance.StartupImpact.BaseGameProfiler.Start(key);

    public static void StopBaseGameProfiler(string key) =>
        // LoadingProgressMod.DevMessage($"Stopping base game profiler for {key}");
        _ = LoadingProgressMod.instance.StartupImpact.BaseGameProfiler.Stop(key);

    /// <summary>
    /// Translates a category string, supporting optional parameter after '|'.
    /// If the string contains '|', the part before is used as the key, the part after as a parameter.
    /// </summary>
    public static string TranslateCategory(string? category)
    {
        if (category == null)
        {
            return string.Empty;
        }

        var pipeIdx = category.IndexOf('|', StringComparison.Ordinal);
        if (pipeIdx < 0)
        {
            return category.Translate();
        }
        var key = category[..pipeIdx];
        var param = category[(pipeIdx + 1)..];
        return key.Translate(param);
    }

    /// <summary>
    /// Derives a stable color from an arbitrary string (e.g. a mod package ID), for cases where
    /// there's no curated color available, such as base-game profiler categories or per-mod bars.
    /// </summary>
    public static Color HashColor(string key)
    {
        var hash = key.GetHashCode(StringComparison.Ordinal);
        return new Color(
            (hash & 0xff) / 255f,
            ((hash >> 8) & 0xff) / 255f,
            ((hash >> 16) & 0xff) / 255f
        );
    }
}
