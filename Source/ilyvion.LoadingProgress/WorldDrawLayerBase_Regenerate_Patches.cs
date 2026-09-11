using RimWorld.Planet;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(WorldDrawLayerBase), nameof(WorldDrawLayerBase.Regenerate))]
internal static class WorldDrawLayerBase_Regenerate_Patches
{
    private static void Prefix(WorldDrawLayerBase __instance) =>
        InGameLoadingSession.OnWorldDrawLayerRegenerationStarted(__instance);
}
