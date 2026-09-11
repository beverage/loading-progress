namespace ilyvion.LoadingProgress;

// The respawningAfterLoad flag is only ever set from Map.FinalizeLoading's post-load respawn
// loop; checking it first (before touching session/phase state) keeps both patches a no-op on the
// ordinary in-play spawning path, which calls these very frequently with the flag false.
[HarmonyPatch(typeof(GenSpawn))]
internal static class GenSpawn_Spawn_Patches
{
    [HarmonyPatch(
        nameof(GenSpawn.Spawn),
        [
            typeof(Thing),
            typeof(IntVec3),
            typeof(Map),
            typeof(Rot4),
            typeof(WipeMode),
            typeof(bool),
            typeof(bool),
        ]
    )]
    [HarmonyPostfix]
    private static void SpawnPostfix(bool respawningAfterLoad)
    {
        if (!respawningAfterLoad)
        {
            return;
        }
        InGameLoadingSession.OnThingSpawnedAfterLoad();
    }

    [HarmonyPatch(nameof(GenSpawn.SpawnBuildingAsPossible))]
    [HarmonyPostfix]
    private static void SpawnBuildingAsPossiblePostfix(bool respawningAfterLoad)
    {
        if (!respawningAfterLoad)
        {
            return;
        }
        InGameLoadingSession.OnThingSpawnedAfterLoad();
    }
}
