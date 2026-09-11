namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(Thing), nameof(Thing.PostMapInit))]
internal static class Thing_PostMapInit_Patches
{
    private static void Postfix(Thing __instance) =>
        InGameLoadingSession.OnThingPostMapInit(__instance.Map);
}
