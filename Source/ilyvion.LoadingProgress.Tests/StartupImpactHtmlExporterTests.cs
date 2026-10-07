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
        StartupImpactHtmlExporter.BuildReport(
            Session,
            viewData,
            new Dictionary<string, Color>(),
            Color.gray,
            showBaseGameOffThreadImpact: true,
            secondsOnly: false
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

        foreach (
            var (key, text) in new[]
            {
                ("baseGameBreakdownText", viewData.BaseGameBreakdownText),
                ("largestBaseGameStepText", viewData.LargestBaseGameStepText),
                ("remainingBreakdownText", viewData.RemainingBreakdownText),
                ("largestRemainingEntryText", viewData.LargestRemainingEntryText),
            }
        )
        {
            Expect.IsNotNull(text);
            Expect.IsTrue(
                html.Contains($"\"{key}\":\"{Escaped(text!)}\",", StringComparison.Ordinal)
            );
        }
        foreach (
            var (key, ms) in new[]
            {
                ("windowMs", viewData.TotalWindow),
                ("remainingMs", viewData.RemainingLoadingTime),
                ("remainingBarSpanMs", viewData.RemainingBarSpan),
            }
        )
        {
            var value = ms.ToString("0.###", CultureInfo.InvariantCulture);
            Expect.IsTrue(html.Contains($"\"{key}\":{value},", StringComparison.Ordinal));
        }
    }

    // A string as the report's data writes it.
    private static string Escaped(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
