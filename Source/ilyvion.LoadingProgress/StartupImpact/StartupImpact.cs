namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class StartupImpact
{
    private int _activeThreadId;
    private readonly ProfilerStopwatch _loadingProfiler;

    public ModInfoList Modlist { get; } = new();

    /// <summary>
    /// The total loading time, set only after FinishLoading() is called.
    /// </summary>
    public float TotalLoadingTime { get; private set; }
    public Profiler BaseGameProfiler { get; }

    /// <summary>
    /// Whether TrackStartupLoadingImpact was on when the mod was constructed, i.e. for the
    /// whole duration of this startup. Unlike LoadingProgressMod.Settings.TrackStartupLoadingImpact,
    /// this doesn't change if the user flips the setting later in the same session.
    /// </summary>
    public bool WasTrackingEnabledAtStartup { get; }

    public StartupImpact()
    {
        _activeThreadId = Environment.CurrentManagedThreadId;

        WasTrackingEnabledAtStartup = LoadingProgressMod.Settings.TrackStartupLoadingImpact;

        BaseGameProfiler = new Profiler("base game");
        _loadingProfiler = new ProfilerStopwatch("loading");

        if (WasTrackingEnabledAtStartup)
        {
            _loadingProfiler.Start("loading");
        }
    }

    private bool _loadingTimeMeasured;

    public void FinishLoading()
    {
        if (!_loadingTimeMeasured)
        {
            _loadingTimeMeasured = true;
            _ = _loadingProfiler.Stop("loading");
            TotalLoadingTime = _loadingProfiler.Total;

            LoadingProgressMod.instance.harmony.UnpatchCategory(
                Assembly.GetExecutingAssembly(),
                "StartupImpact"
            );

            if (
                WasTrackingEnabledAtStartup
                && LoadingProgressMod.Settings.AutoSaveStartupImpactReport
            )
            {
                // FinishLoading can run off the main thread, and Scribe.saver is
                // global state also used from the main thread; defer the save.
                LongEventHandler.ExecuteWhenFinished(static () =>
                {
                    try
                    {
                        Dialog.StartupImpactSessionStorage.Save(
                            Dialog.StartupImpactSessionData.FromCurrentSession()
                        );
                    }
                    catch (Exception e)
                    {
                        LoadingProgressMod.Error("Failed to auto-save startup impact report: " + e);
                    }
                });
            }
        }
    }

    public void UpdateActiveThreadId() => _activeThreadId = Environment.CurrentManagedThreadId;

    public bool IsActiveThread() => Environment.CurrentManagedThreadId == _activeThreadId;
}
