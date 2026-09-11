using RimWorld.Planet;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.GeneratePlanetLayer))]
internal static class WorldGenerator_GeneratePlanetLayer_Patches
{
    private static void Prefix(PlanetLayer layer) =>
        InGameLoadingSession.OnWorldGenLayerStarted(layer.Def);
}
