namespace ilyvion.LoadingProgress.FasterGameLoading;

internal static class FasterGameLoadingUtils
{
    internal const string FasterGameLoadingPackageId = "taranchuk.fastergameloading";

    private static bool? _hasFasterGameLoading;
    public static bool HasFasterGameLoading
    {
        get
        {
            _hasFasterGameLoading ??= ModsConfig.ActiveModsInLoadOrder.Any(mod =>
                IsFasterGameLoadingPackageId(mod.PackageId)
            );
            return _hasFasterGameLoading.Value;
        }
    }

    /// <summary>
    /// Whether a package id, as the mod list reports it, names Faster Game Loading.
    /// </summary>
    /// <remarks>
    /// The game appends <see cref="ModMetaData.SteamModPostfix"/> to a Workshop mod's id
    /// whenever a local copy with the same id is installed, so the id is compared without it.
    /// </remarks>
    internal static bool IsFasterGameLoadingPackageId(string? packageId)
    {
        if (string.IsNullOrEmpty(packageId))
        {
            return false;
        }

        var id = packageId!;
        if (id.EndsWith(ModMetaData.SteamModPostfix, StringComparison.OrdinalIgnoreCase))
        {
            id = id[..^ModMetaData.SteamModPostfix.Length];
        }

        return id.Equals(FasterGameLoadingPackageId, StringComparison.OrdinalIgnoreCase);
    }

    public static HashSet<ModContentPack>? LoadedMods
    {
        get
        {
            field ??=
                AccessTools
                    .Field("FasterGameLoading.ModContentPack_ReloadContentInt_Patch:loadedMods")
                    ?.GetValue(null) as HashSet<ModContentPack>;
            return field;
        }
    }

    public static bool FasterGameLoadingEarlyModContentLoadingIsFinished =>
        FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate
        || LoadingProgressWindow.CurrentStage >= LoadingStage.ExecuteToExecuteWhenFinished2;

    public static T? GetFasterGameLoadingSetting<T>(string settingName) =>
        AccessTools
            .Field("FasterGameLoading.FasterGameLoadingSettings:" + settingName)
            ?.GetValue(null)
            is T value
            ? value
            : default;

    private static bool? _earlyModContentLoading;
    public static bool EarlyModContentLoading
    {
        get
        {
            _earlyModContentLoading ??= GetFasterGameLoadingSetting<bool>("earlyModContentLoading");
            return _earlyModContentLoading.Value;
        }
    }
}
