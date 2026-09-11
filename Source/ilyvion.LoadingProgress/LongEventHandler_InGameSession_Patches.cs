namespace ilyvion.LoadingProgress;

// LongEventsUpdate runs every frame regardless of whether an event is currently displayed,
// unlike DrawLongEventWindowContents (which only fires while currentEvent != null), so it's the
// only reliable place to observe "the queue went empty" and end a session.
[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsUpdate))]
internal static class LongEventHandler_LongEventsUpdate_Patches
{
    private static void Postfix() => InGameLoadingSession.Update();
}

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.ClearQueuedEvents))]
internal static class LongEventHandler_ClearQueuedEvents_Patches
{
    private static void Postfix() => InGameLoadingSession.SignalReset();
}

[HarmonyPatch(typeof(Scribe), nameof(Scribe.ForceStop))]
internal static class Scribe_ForceStop_Patches
{
    private static void Postfix() => InGameLoadingSession.SignalReset();
}
