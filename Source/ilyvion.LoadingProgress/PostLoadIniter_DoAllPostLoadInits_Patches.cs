using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

// Same technique as CrossRefHandler_ResolveAllCrossReferences_Patches: PostLoadIniter's
// DoAllPostLoadInits() also emits a single DeepProfiler label around its whole loop, so per-item
// progress needs a transpiler tick after each IExposable.ExposeData() call instead.
[HarmonyPatch(typeof(PostLoadIniter))]
internal static class PostLoadIniter_DoAllPostLoadInits_Patches
{
    [HarmonyPatch(nameof(PostLoadIniter.DoAllPostLoadInits))]
    [HarmonyTranspiler]
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> DoAllPostLoadInitsTranspiler(
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
                "PostLoadIniter.DoAllPostLoadInits: Could not find a call to IExposable.ExposeData."
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
