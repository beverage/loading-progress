using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;
using ilyvion.LoadingProgress.StartupImpact.Dialog.Export;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactHtmlExporterTests
{
    // Loading stops the clock at 90.6 s and the menu is usable at 110.6 s; two stages leave
    // time no step accounts for.
    private static readonly StartupImpactSessionData Session = StartupImpactSessionData.FromValues(
        90600f,
        110600f,
        new() { ["LoadingProgress.StartupImpact.AbstractFilesystemClearAllCache"] = 1000f },
        [],
        [new("LoadingDefs", 1500f, 500f), new("AtlasBaking", 800f, 600f)]
    );

    private static string Report(StartupImpactSessionViewData viewData) =>
        Report(Session, viewData, secondsOnly: false);

    private static string Report(
        StartupImpactSessionData session,
        StartupImpactSessionViewData viewData,
        bool secondsOnly
    ) =>
        StartupImpactHtmlExporter.BuildReport(
            session,
            viewData,
            new Dictionary<string, Color>(),
            Color.gray,
            showBaseGameOffThreadImpact: true,
            secondsOnly
        );

    // The report carries the remaining bar's segments, each with its colour and time, and the
    // time to the menu the top bar spans.
    [Test]
    public static void TheReportCarriesTheRemainingBar()
    {
        var viewData = new StartupImpactSessionViewData(Session);
        var html = Report(viewData);

        Expect.IsTrue(html.Contains("\"timeToMenuMs\":110600,", StringComparison.Ordinal));
        Expect.IsTrue(html.Contains("\"remainingTitle\":", StringComparison.Ordinal));
        Expect.IsTrue(html.Contains("id=\"remainingBar\"", StringComparison.Ordinal));
        Expect.AreEqual(3, viewData.RemainingByStage.Count);
        foreach (var entry in viewData.RemainingByStage)
        {
            var valueMs = entry.Ms.ToString("0.###", CultureInfo.InvariantCulture);
            Expect.IsTrue(
                html.Contains(
                    $"\"label\":\"{entry.Label}\",\"color\":\"#",
                    StringComparison.Ordinal
                )
            );
            Expect.IsTrue(html.Contains($"\"valueMs\":{valueMs}}}", StringComparison.Ordinal));
        }
    }

    // The report's mod table takes its shared scale from both of a row's bars, as the window's
    // does, and every mod carries its time on other threads for it.
    [Test]
    public static void TheReportsTableScaleCountsTimeOnOtherThreads()
    {
        var html = Report(new StartupImpactSessionViewData(Session));

        Expect.IsTrue(
            html.Contains(
                "max = Math.max(max, mod.totalImpactMs, mod.offThreadTotalImpactMs);",
                StringComparison.Ordinal
            )
        );
    }

    // The report's folded headings carry a detail as the window's do, and every bar for time
    // on other threads says so.
    [Test]
    public static void TheReportsSectionsCarryTheWindowsDetailsAndTooltips()
    {
        var html = Report(new StartupImpactSessionViewData(Session));

        Expect.IsTrue(html.Contains("id=\"baseGameDetail\"", StringComparison.Ordinal));
        Expect.IsTrue(html.Contains("id=\"remainingDetail\"", StringComparison.Ordinal));
        var tip = "LoadingProgress.StartupImpact.OnOtherThreads.Tip".Translate().ToString();
        Expect.IsTrue(html.Contains($"\"onOtherThreadsTip\":\"{tip}\"", StringComparison.Ordinal));
        foreach (
            var call in new[]
            {
                "renderBar(offBar, mod.offThreadMetrics, rowMaxImpact, DATA.strings.onOtherThreadsTip);",
                "renderBar(offBar, phase.offThreadSegments, maxImpact, DATA.strings.onOtherThreadsTip);",
                "renderBar(offBar, DATA.baseGame.offThreadSegments, maxImpact, DATA.strings.onOtherThreadsTip);",
            }
        )
        {
            Expect.IsTrue(html.Contains(call, StringComparison.Ordinal));
        }
    }

    // The report's script used to work out the folded sections' breakdowns, their largest
    // entries and the remaining total again, from a separately exported line count, so a
    // change to the window's had to be copied into it. It now shows the window's own.
    [Test]
    public static void TheReportShowsTheWindowsOwnBreakdownsAndTotals()
    {
        var viewData = new StartupImpactSessionViewData(Session);
        var html = Report(viewData);

        ExpectTexts(html, viewData.Texts(secondsOnly: false));
        foreach (
            var (key, ms) in new[]
            {
                ("windowMs", viewData.TotalWindow),
                ("remainingMs", viewData.RemainingLoadingTime),
            }
        )
        {
            var value = ms.ToString("0.###", CultureInfo.InvariantCulture);
            Expect.IsTrue(html.Contains($"\"{key}\":{value},", StringComparison.Ordinal));
        }
    }

    // The report writes the section texts as it writes its own times: a report exported with
    // the seconds-only setting on used to carry them as the window had them when it opened.
    [Test]
    public static void TheReportWritesItsSectionTextsAsItWritesItsTimes()
    {
        // A step and a stage's remaining time of over a minute, which the two ways of writing
        // times write differently.
        var session = StartupImpactSessionData.FromValues(
            100000f,
            0f,
            new() { ["LoadingProgress.StartupImpact.GarbageCollection"] = 62300f },
            [],
            [new("LoadingDefs", 80000f, 17700f)]
        );
        var viewData = new StartupImpactSessionViewData(session);
        Expect.AreNotEqual(
            viewData.Texts(secondsOnly: true).LargestRemainingEntry,
            viewData.Texts(secondsOnly: false).LargestRemainingEntry
        );

        foreach (var secondsOnly in new[] { true, false })
        {
            ExpectTexts(Report(session, viewData, secondsOnly), viewData.Texts(secondsOnly));
        }
    }

    // The report carries each of the section texts as it is.
    private static void ExpectTexts(string html, StartupImpactSessionViewData.SectionTexts texts)
    {
        foreach (
            var (key, text) in new[]
            {
                ("baseGameBreakdownText", texts.BaseGameBreakdown),
                ("largestBaseGameStepText", texts.LargestBaseGameStep),
                ("remainingBreakdownText", texts.RemainingBreakdown),
                ("largestRemainingEntryText", texts.LargestRemainingEntry),
            }
        )
        {
            Expect.IsNotNull(text);
            Expect.IsTrue(
                html.Contains($"\"{key}\":\"{Escaped(text!)}\",", StringComparison.Ordinal)
            );
        }
    }

    // A string as the report's data writes it.
    private static string Escaped(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
