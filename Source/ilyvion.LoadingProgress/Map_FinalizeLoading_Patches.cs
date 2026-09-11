namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(Map), nameof(Map.FinalizeLoading))]
internal static class Map_FinalizeLoading_Patches
{
    private static void Prefix(Map __instance) =>
        InGameLoadingSession.OnMapFinalizeLoadingStarted(__instance.loadedFullThings.Count);
}
