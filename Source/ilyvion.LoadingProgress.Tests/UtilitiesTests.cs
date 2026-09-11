using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class UtilitiesTests
{
    [Test]
    public static void ShouldUseStandardWindowIsTrueWhenTheEventAllowsItAndTheUiIsAvailable() =>
        Assert
            .That(
                Utilities.ShouldUseStandardWindow(
                    eventUseStandardWindow: true,
                    uiRootAvailable: true,
                    windowStackAvailable: true
                )
            )
            .Is.True();

    [Test]
    public static void ShouldUseStandardWindowIsFalseWhenTheEventDoesNotAllowIt() =>
        // Async/enumerator events (canEverUseStandardWindow false, or doAsynchronously true)
        // always draw directly, regardless of UI availability.
        Assert
            .That(
                Utilities.ShouldUseStandardWindow(
                    eventUseStandardWindow: false,
                    uiRootAvailable: true,
                    windowStackAvailable: true
                )
            )
            .Is.False();

    [Test]
    public static void ShouldUseStandardWindowIsFalseWhenTheUiRootIsUnavailable() =>
        // Can happen for a frame around a scene transition; falls back to drawing directly
        // rather than calling into a window stack that may not exist yet.
        Assert
            .That(
                Utilities.ShouldUseStandardWindow(
                    eventUseStandardWindow: true,
                    uiRootAvailable: false,
                    windowStackAvailable: true
                )
            )
            .Is.False();

    [Test]
    public static void ShouldUseStandardWindowIsFalseWhenTheWindowStackIsUnavailable() =>
        Assert
            .That(
                Utilities.ShouldUseStandardWindow(
                    eventUseStandardWindow: true,
                    uiRootAvailable: true,
                    windowStackAvailable: false
                )
            )
            .Is.False();
}
