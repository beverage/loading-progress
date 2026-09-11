namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.SetCurrentEventText))]
internal static class LongEventHandler_SetCurrentEventText_Patches
{
    private static void Postfix() => InGameLoadingSession.OnSetCurrentEventText();
}
