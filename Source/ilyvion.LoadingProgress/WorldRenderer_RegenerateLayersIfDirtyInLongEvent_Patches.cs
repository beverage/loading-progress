using RimWorld.Planet;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(WorldRenderer), nameof(WorldRenderer.RegenerateLayersIfDirtyInLongEvent))]
internal static class WorldRenderer_RegenerateLayersIfDirtyInLongEvent_Patches
{
    private static void Prefix(WorldRenderer __instance) =>
        InGameLoadingSession.OnPlanetRegenerationQueued(
            InGameLoadingSession.CountDirtyVisibleLayers(
                __instance.AllDrawLayers.Select(l => (l.Dirty, l.Visible))
            )
        );
}
