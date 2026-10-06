using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadModXML))]
[HarmonyPatchCategory("StartupImpact")]
internal static class LoadedModManager_LoadModXML
{
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i =>
            i.Calls(AccessTools.Method(typeof(ModContentPack), nameof(ModContentPack.LoadDefs)))
        );
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error(
                "LoadedLanguage.LoadMetadata: Could not find a call to ModContentPack.LoadDefs."
            );
            return original;
        }

        // Each mod's defs load through LoadDefsTimed, which takes the same arguments.
        _ = codeMatcher.Set(
            OpCodes.Call,
            AccessTools.Method(typeof(LoadedModManager_LoadModXML), nameof(LoadDefsTimed))
        );

        return codeMatcher.Instructions();
    }

    /// <summary>
    /// Loads one mod's defs, timed under that mod.
    /// </summary>
    /// <remarks>
    /// <see cref="ModContentPack.LoadDefs"/> is an iterator, so its work happens as its defs
    /// are read, and only the reading is timed. The call before it, where other mods' hooks on
    /// LoadDefs run, stays outside the category. The category closes in a finally: the engine
    /// catches a mod whose defs fail to load and goes on to the next mod, and a category left
    /// open would take in that mod's later steps.
    /// </remarks>
    internal static IEnumerable<LoadableXmlAsset> LoadDefsTimed(
        ModContentPack modContentPack,
        bool hotReload
    )
    {
        var defs = modContentPack.LoadDefs(hotReload);
        StartupImpactProfilerUtil.StartModProfiler(
            modContentPack,
            "LoadingProgress.StartupImpact.LoadDefs"
        );
        try
        {
            return [.. defs];
        }
        finally
        {
            StartupImpactProfilerUtil.StopModProfiler(
                modContentPack,
                "LoadingProgress.StartupImpact.LoadDefs"
            );
        }
    }
}
