namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(MapFileCompressor), nameof(MapFileCompressor.ThingsToSpawnAfterLoad))]
internal static class MapFileCompressor_ThingsToSpawnAfterLoad_Patches
{
    private static void Postfix(IEnumerable<Thing> __result) =>
        InGameLoadingSession.OnCompressedThingsCounted(__result.Count());
}
