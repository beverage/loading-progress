using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

// CrossRefHandler.ResolveAllCrossReferences() emits a single DeepProfiler label around its whole
// loop, so per-item progress needs a transpiler tick after each IExposable.ExposeData() call
// instead - the same technique already used for DirectXmlCrossRefLoader's WantedRef.Apply loop in
// CurrentMod_Patches.cs.
[HarmonyPatch(typeof(CrossRefHandler))]
internal static class CrossRefHandler_ResolveAllCrossReferences_Patches
{
    [HarmonyPatch(nameof(CrossRefHandler.ResolveAllCrossReferences))]
    [HarmonyTranspiler]
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> ResolveAllCrossReferencesTranspiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i =>
            i.Calls(AccessTools.Method(typeof(IExposable), nameof(IExposable.ExposeData)))
        );
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error(
                "CrossRefHandler.ResolveAllCrossReferences: Could not find a call to IExposable.ExposeData."
            );
            return original;
        }

        _ = codeMatcher
            .Advance(1)
            .Insert([
                new(
                    OpCodes.Call,
                    AccessTools.Method(
                        typeof(InGameLoadingSession),
                        nameof(InGameLoadingSession.OnInitializingSubProgressItemProcessed)
                    )
                ),
            ]);

        return codeMatcher.Instructions();
    }
}
