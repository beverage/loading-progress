using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadModXML))]
[HarmonyPatchCategory("StartupImpact")]
internal static class LoadedModManager_LoadModXML
{
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    internal static IEnumerable<CodeInstruction> Transpiler(
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
                "LoadedModManager.LoadModXML: Could not find a call to ModContentPack.LoadDefs."
            );
            return original;
        }

        // The call's arguments are the mod and hotReload, so the instruction two before it
        // loads the mod.
        var loadMod = codeMatcher.InstructionAt(-2);
        if (!loadMod.IsLdloc())
        {
            LoadingProgressMod.Error(
                "LoadedModManager.LoadModXML: Could not find the mod ModContentPack.LoadDefs is called on."
            );
            return original;
        }

        // The call stays where other mods' transpilers look for it. The defs it returns are
        // read through ReadDefsTimed, which hands them on to the list they were going to.
        _ = codeMatcher
            .Advance(1)
            .InsertAndAdvance([
                new(loadMod.opcode, loadMod.operand),
                new(
                    OpCodes.Call,
                    AccessTools.Method(typeof(LoadedModManager_LoadModXML), nameof(ReadDefsTimed))
                ),
            ]);

        return codeMatcher.Instructions();
    }

    /// <summary>
    /// Reads the defs <see cref="ModContentPack.LoadDefs"/> returned for one mod, timed under
    /// that mod, and returns them read.
    /// </summary>
    /// <remarks>
    /// LoadDefs is an iterator, so its work happens as its defs are read, and only the reading
    /// is timed. The call before it, where other mods' hooks on LoadDefs run, stays outside the
    /// category. The category closes in a finally: the engine catches a mod whose defs fail to
    /// load and goes on to the next mod, and a category left open would take in that mod's
    /// later steps.
    /// </remarks>
    internal static IEnumerable<LoadableXmlAsset> ReadDefsTimed(
        IEnumerable<LoadableXmlAsset> defs,
        ModContentPack modContentPack
    )
    {
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
