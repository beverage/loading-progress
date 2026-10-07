using System.Text;

namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

internal sealed class StartupImpactSessionViewData
{
    public static readonly string[] CategoriesTotal =
    [
        "LoadingProgress.StartupImpact.Total.Mods",
        "LoadingProgress.StartupImpact.Total.ModsHidden",
        "LoadingProgress.StartupImpact.Total.BaseGame",
        "LoadingProgress.StartupImpact.Total.Others",
    ];

    /// <summary>
    /// One part of the remaining time: the loading stage it fell in (or the time after loading
    /// finished, keyed <see cref="AfterLoadingKey"/>), its text as shown, and how much.
    /// </summary>
    internal sealed record RemainingEntry(string Key, string Label, float Ms);

    internal const string AfterLoadingKey = "LoadingProgress.StartupImpact.Remaining.AfterLoading";

    // The second delayed-initialization pass borrows the first one's text, and the remaining
    // time lists both, so it is told apart there.
    private const string SecondPassKey = "LoadingProgress.StartupImpact.Remaining.SecondPass";

    // How many lines a breakdown in a tooltip lists, largest first; the rest are counted.
    internal const int BreakdownLines = 10;

    private readonly StartupImpactSessionData sessionData;
    private readonly List<StartupImpactSessionModViewData> modViewData;
    private float hiddenModsLoadingTime;

    private readonly List<string> categories = [];
    private readonly List<string> categoriesNonMods = [];
    private readonly List<float> metricsNonMods = [];
    private readonly List<float> metricsOffThreadNonMods = [];
    private readonly List<float> metricsTotal = [];
    private readonly Dictionary<string, Color> categoryColorsNonMods = [];
    private readonly List<string> categoriesMods = [];
    private readonly List<float> metricsMods = [];
    private readonly Dictionary<string, Color> categoryColorsMods = [];
    private readonly List<RemainingEntry> remainingByStage = [];
    private readonly List<string> categoriesRemaining = [];
    private readonly List<float> metricsRemaining = [];
    private readonly Dictionary<string, Color> categoryColorsRemaining = [];

    internal IReadOnlyList<StartupImpactSessionModViewData> ModViewData => modViewData.AsReadOnly();

    public float BasegameLoadingTime { get; private set; }
    public float OffThreadBasegameLoadingTime { get; private set; }
    public float ModsLoadingTime { get; private set; }

    /// <summary>
    /// The mod table's shared scale: the largest time any visible mod has on the loading
    /// thread or on other threads.
    /// </summary>
    public float MaxImpact { get; private set; }

    /// <summary>
    /// A table's shared scale for rows of time on the loading thread and on other threads:
    /// the largest of either among the rows that are shown, so each row's two bars share the
    /// scale with every other row. A hidden row does not set it.
    /// </summary>
    internal static float SharedScale(
        IEnumerable<(float OnThread, float OffThread, bool Hidden)> rows
    ) =>
        rows.Where(row => !row.Hidden)
            .Select(row => Math.Max(row.OnThread, row.OffThread))
            .DefaultIfEmpty(0f)
            .Max();

    public IReadOnlyList<string> Categories => categories.AsReadOnly();
    public IReadOnlyList<string> CategoriesNonMods => categoriesNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsNonMods => metricsNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsOffThreadNonMods => metricsOffThreadNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsTotal => metricsTotal.AsReadOnly();
    public IReadOnlyDictionary<string, Color> CategoryColorsNonMods =>
        categoryColorsNonMods.AsReadOnly();
    public IReadOnlyList<string> CategoriesMods => categoriesMods.AsReadOnly();
    public IReadOnlyList<float> MetricsMods => metricsMods.AsReadOnly();
    public IReadOnlyDictionary<string, Color> CategoryColorsMods => categoryColorsMods.AsReadOnly();
    public IReadOnlyList<string> CategoriesRemaining => categoriesRemaining.AsReadOnly();
    public IReadOnlyList<float> MetricsRemaining => metricsRemaining.AsReadOnly();
    public IReadOnlyDictionary<string, Color> CategoryColorsRemaining =>
        categoryColorsRemaining.AsReadOnly();

    /// <summary>
    /// The span the totals bar covers, which the window's title gives as the startup time: see
    /// <see cref="Span(float, float, float, float)"/>.
    /// </summary>
    public float TotalWindow { get; private set; }

    /// <summary>
    /// The remaining part of the startup time: what is left of the window once the mods,
    /// hidden or not, and the base game have had theirs. The totals bar's last segment.
    /// </summary>
    public float RemainingLoadingTime =>
        metricsTotal.Count == CategoriesTotal.Length ? metricsTotal[^1] : 0f;

    /// <summary>
    /// What the remaining bar spans: the remaining total, or the sum of its entries when the
    /// stages add up to more, since the two are measured separately and can differ a little.
    /// </summary>
    public float RemainingBarSpan => Math.Max(RemainingLoadingTime, metricsRemaining.Sum());

    /// <summary>
    /// What the base game's bars span: with the off-thread bar shown, the longer of its time
    /// on the loading thread and its time on other threads, so the two bars share one scale;
    /// without it, the loading-thread time alone.
    /// </summary>
    internal static float BaseGameBarSpan(
        float onThreadMs,
        float offThreadMs,
        bool offThreadShown
    ) => offThreadShown ? Math.Max(onThreadMs, offThreadMs) : onThreadMs;

    /// <summary>
    /// How the remaining time splits by loading stage, largest first, with what came after
    /// loading finished as an entry of its own. Empty for sessions saved before stages were
    /// kept.
    /// </summary>
    public IReadOnlyList<RemainingEntry> RemainingByStage => remainingByStage.AsReadOnly();

    /// <summary>
    /// The remaining split as text, for the totals bar's remaining segment and the folded
    /// remaining heading, or null when there is nothing to say.
    /// </summary>
    public string? RemainingBreakdownText { get; private set; }

    /// <summary>
    /// The remaining time's largest entry and its time, for the folded remaining heading, or
    /// null when there are no entries.
    /// </summary>
    public string? LargestRemainingEntryText { get; private set; }

    /// <summary>
    /// The base game's largest steps as text, for the totals bar's base-game segment and the
    /// folded base-game heading, or null when it has no steps.
    /// </summary>
    public string? BaseGameBreakdownText { get; private set; }

    /// <summary>
    /// The base game's largest step and its time, for the folded base-game heading, or null
    /// when it has no steps.
    /// </summary>
    public string? LargestBaseGameStepText { get; private set; }

    private readonly Dictionary<string, string> totalsTooltipDetails = [];

    /// <summary>
    /// What the totals bar's segments add to their tooltips: the base game's largest steps
    /// and the remaining time by stage.
    /// </summary>
    public IReadOnlyDictionary<string, string> TotalsTooltipDetails =>
        totalsTooltipDetails.AsReadOnly();

    /// <summary>
    /// The steps with time in them, largest first, at most <paramref name="count"/> of them.
    /// </summary>
    internal static IReadOnlyList<(string Category, float Ms)> LargestSteps(
        IReadOnlyList<string> categories,
        IReadOnlyList<float> metrics,
        int count
    ) =>
        [
            .. categories
                .Select((category, i) => (Category: category, Ms: metrics[i]))
                .Where(step => step.Ms >= 1f)
                .OrderByDescending(step => step.Ms)
                .Take(count),
        ];

    private void RebuildTooltipDetails()
    {
        totalsTooltipDetails.Clear();
        if (BaseGameBreakdownText != null)
        {
            totalsTooltipDetails["LoadingProgress.StartupImpact.Total.BaseGame"] =
                BaseGameBreakdownText;
        }
        if (RemainingBreakdownText != null)
        {
            totalsTooltipDetails["LoadingProgress.StartupImpact.Total.Others"] =
                RemainingBreakdownText;
        }
    }

    /// <summary>
    /// A breakdown as tooltip text: the header, then up to <see cref="BreakdownLines"/> lines,
    /// largest first, and how many more there are when that leaves some out.
    /// </summary>
    internal static string Breakdown(string header, IReadOnlyList<(string Label, float Ms)> lines)
    {
        var sb = new StringBuilder(header);
        foreach (var (label, ms) in lines.Take(BreakdownLines))
        {
            _ = sb.Append('\n').Append(label).Append(": ").Append(ProfilerBar.TimeText(ms));
        }
        if (lines.Count > BreakdownLines)
        {
            _ = sb.Append('\n')
                .Append(
                    "LoadingProgress.StartupImpact.Breakdown.More".Translate(
                        lines.Count - BreakdownLines
                    )
                );
        }
        return sb.ToString();
    }

    public StartupImpactSessionViewData(StartupImpactSessionData sessionData)
    {
        this.sessionData = sessionData;
        modViewData = [.. sessionData.Mods.Select(mod => new StartupImpactSessionModViewData(mod))];

        CalculateBaseGameStats();
        CalculateModStats();
        CalculateRemainingByStage();

        foreach (var modView in modViewData)
        {
            modView.Initialize(this);
        }
    }

    public void CalculateModStats()
    {
        ModsLoadingTime = 0;
        hiddenModsLoadingTime = 0;
        MaxImpact = SharedScale(
            modViewData.Select(modView =>
                (
                    modView.ModData.TotalImpact,
                    modView.ModData.OffThreadTotalImpact,
                    modView.HideInUi
                )
            )
        );

        HashSet<string> categorySet = [];
        foreach (var modView in modViewData)
        {
            if (modView.HideInUi)
            {
                hiddenModsLoadingTime += modView.ModData.TotalImpact;
            }
            else
            {
                ModsLoadingTime += modView.ModData.TotalImpact;
            }

            foreach (var entry in modView.ModData.Metrics)
            {
                _ = categorySet.Add(entry.Key);
            }

            foreach (var entry in modView.ModData.OffThreadMetrics)
            {
                _ = categorySet.Add(entry.Key);
            }
        }

        categories.Clear();
        categories.AddRange(categorySet.OrderBy(category => category));

        // A session with no loading time takes its timed steps as one. Any other keeps the
        // stored loading time as the point the clock stopped, which is what the session file
        // and external tools read, even when the steps come to more: the bar widens instead.
        var totalLoadingTime = ModsLoadingTime + hiddenModsLoadingTime + BasegameLoadingTime;
        TotalWindow = Span(
            sessionData.LoadingTime,
            sessionData.TimeToMenu,
            PostLoadAttributedTime(sessionData),
            totalLoadingTime
        );
        if (sessionData.LoadingTime == 0)
        {
            sessionData.OverrideLoadingTime(totalLoadingTime);
        }

        metricsTotal.Clear();
        metricsTotal.AddRange([
            ModsLoadingTime,
            hiddenModsLoadingTime,
            BasegameLoadingTime,
            Math.Max(0, TotalWindow - totalLoadingTime),
        ]);

        categoriesMods.Clear();
        metricsMods.Clear();
        foreach (
            var modView in modViewData
                .Where(m => !m.HideInUi && m.ModData.TotalImpact > 0)
                .OrderByDescending(m => m.ModData.TotalImpact)
        )
        {
            var name = modView.ModData.ModName;
            categoryColorsMods[name] = StartupImpactProfilerUtil.HashColor(
                modView.ModData.ModPackageId
            );
            categoriesMods.Add(name);
            metricsMods.Add(modView.ModData.TotalImpact);
        }
        RebuildTooltipDetails();
    }

    public void CalculateBaseGameStats()
    {
        categoriesNonMods.Clear();
        metricsNonMods.Clear();
        metricsOffThreadNonMods.Clear();
        BasegameLoadingTime = 0;

        foreach (var entry in sessionData.Metrics)
        {
            var cat = entry.Key;

            categoryColorsNonMods[cat] = StartupImpactProfilerUtil.HashColor(cat);
            categoriesNonMods.Add(cat);
            metricsNonMods.Add(entry.Value);
            BasegameLoadingTime += entry.Value;
            metricsOffThreadNonMods.Add(
                sessionData.OffThreadMetrics.TryGetValue(cat, out var offValue) ? offValue : 0f
            );
        }

        foreach (var entry in sessionData.OffThreadMetrics)
        {
            if (categoriesNonMods.Contains(entry.Key))
            {
                continue;
            }

            categoryColorsNonMods[entry.Key] = StartupImpactProfilerUtil.HashColor(entry.Key);
            categoriesNonMods.Add(entry.Key);
            metricsNonMods.Add(0f);
            metricsOffThreadNonMods.Add(entry.Value);
        }

        OffThreadBasegameLoadingTime = sessionData.OffThreadTotalImpact;

        BaseGameBreakdownText = null;
        LargestBaseGameStepText = null;
        List<(string Label, float Ms)> steps =
        [
            .. LargestSteps(categoriesNonMods, metricsNonMods, int.MaxValue)
                .Select(step =>
                    (StartupImpactProfilerUtil.TranslateCategory(step.Category), step.Ms)
                ),
        ];
        if (steps.Count > 0)
        {
            BaseGameBreakdownText = Breakdown(
                "LoadingProgress.StartupImpact.Nonmods.BySteps".Translate().ToString(),
                steps
            );
            LargestBaseGameStepText = $"{steps[0].Label}: {ProfilerBar.TimeText(steps[0].Ms)}";
        }
        RebuildTooltipDetails();
    }

    private void CalculateRemainingByStage()
    {
        remainingByStage.Clear();
        categoriesRemaining.Clear();
        metricsRemaining.Clear();
        categoryColorsRemaining.Clear();
        RemainingBreakdownText = null;
        LargestRemainingEntryText = null;

        remainingByStage.AddRange(
            RemainingEntries(
                sessionData.StageTimings,
                sessionData.LoadingTime,
                sessionData.TimeToMenu,
                PostLoadAttributedTime(sessionData)
            )
        );
        if (remainingByStage.Count == 0)
        {
            return;
        }

        foreach (var entry in remainingByStage)
        {
            categoriesRemaining.Add(entry.Label);
            metricsRemaining.Add(entry.Ms);
            categoryColorsRemaining[entry.Label] = StartupImpactProfilerUtil.HashColor(entry.Key);
        }

        RemainingBreakdownText = Breakdown(
            "LoadingProgress.StartupImpact.Remaining.ByStage".Translate().ToString(),
            [.. remainingByStage.Select(entry => (entry.Label, entry.Ms))]
        );
        var largest = remainingByStage[0];
        LargestRemainingEntryText = $"{largest.Label}: {ProfilerBar.TimeText(largest.Ms)}";
        RebuildTooltipDetails();
    }

    /// <summary>
    /// The remaining entries a session's stages and its time to the menu give, largest first:
    /// each stage's wall time less what categories accounted for in it, and what came after
    /// loading finished less what was timed there, the long events and the deferred actions
    /// they queued. Anything under a millisecond is left out.
    /// </summary>
    internal static IReadOnlyList<RemainingEntry> RemainingEntries(
        IEnumerable<StartupImpactStageData> stages,
        float loadingTime,
        float timeToMenu,
        float postLoadAttributedMs
    )
    {
        List<RemainingEntry> entries = [];
        foreach (var stage in stages)
        {
            if (stage.RemainingMs >= 1f)
            {
                var label = StartupImpactSessionIndexEntry.TranslateStage(stage.Stage);
                if (stage.Stage == nameof(LoadingStage.ExecuteToExecuteWhenFinished2))
                {
                    label = SecondPassKey.Translate(label);
                }
                entries.Add(new RemainingEntry(stage.Stage, label, stage.RemainingMs));
            }
        }

        if (timeToMenu > loadingTime)
        {
            var afterLoading = timeToMenu - loadingTime - postLoadAttributedMs;
            if (afterLoading >= 1f)
            {
                entries.Add(
                    new RemainingEntry(AfterLoadingKey, AfterLoadingKey.Translate(), afterLoading)
                );
            }
        }
        entries.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        return entries;
    }

    /// <summary>
    /// The startup time a session is shown and listed by: its time to the main menu when it
    /// recorded one. Without one, as when the startup went into a game or the menu never
    /// settled, it is the loading time plus what was timed after loading, all of which ran
    /// after the clock stopped. It is never less than the timed steps' total, so the totals bar
    /// holds them all.
    /// </summary>
    internal static float Span(
        float loadingTime,
        float timeToMenu,
        float postLoadAttributedMs,
        float timedTotal
    ) => Math.Max(Math.Max(timeToMenu, loadingTime + postLoadAttributedMs), timedTotal);

    /// <summary>
    /// <see cref="Span(float, float, float, float)"/> for a stored session.
    /// </summary>
    internal static float Span(StartupImpactSessionData sessionData) =>
        Span(
            sessionData.LoadingTime,
            sessionData.TimeToMenu,
            PostLoadAttributedTime(sessionData),
            sessionData.Metrics.Sum(entry => entry.Value)
                + sessionData.Mods.Sum(mod => mod.TotalImpact)
        );

    /// <summary>
    /// Time between the end of loading and the main menu that some category did account for:
    /// everything timed under <see cref="PostLoadTracker.Category"/>, whoever ran it.
    /// </summary>
    /// <remarks>
    /// The stage ledger closes when loading ends, so it holds none of this time, and a saved
    /// session has only its metrics to go by. These keep the time the game sat paused in the
    /// background out, as the time to the menu does.
    /// </remarks>
    private static float PostLoadAttributedTime(StartupImpactSessionData sessionData)
    {
        var total = 0f;
        foreach (var entry in sessionData.Metrics)
        {
            if (entry.Key.StartsWith(PostLoadTracker.Category, StringComparison.Ordinal))
            {
                total += entry.Value;
            }
        }
        foreach (var mod in sessionData.Mods)
        {
            foreach (var entry in mod.Metrics)
            {
                if (entry.Key.StartsWith(PostLoadTracker.Category, StringComparison.Ordinal))
                {
                    total += entry.Value;
                }
            }
        }
        return total;
    }
}
