namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(Thing), nameof(Thing.ExposeData))]
internal static class Thing_ExposeData_Patches
{
    private static void Postfix() => InGameLoadingSession.OnThingExposeData();
}
