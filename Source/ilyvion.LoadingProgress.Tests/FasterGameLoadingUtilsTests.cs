using DevTools.Testing;
using ilyvion.LoadingProgress.FasterGameLoading;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class FasterGameLoadingUtilsTests
{
    // The game appends "_steam" to a Workshop mod's package id whenever a local copy with the
    // same id is installed, and the lookup compared the id exactly, so a player with both copies
    // had Faster Game Loading go unseen: its early content loading was neither shown nor taken
    // into account.
    [Test]
    public static void TheWorkshopPostfixDoesNotHideFasterGameLoading() =>
        Expect.IsTrue(
            FasterGameLoadingUtils.IsFasterGameLoadingPackageId("taranchuk.fastergameloading_steam")
        );

    [Test]
    public static void ThePlainPackageIdIsRecognized() =>
        Expect.IsTrue(
            FasterGameLoadingUtils.IsFasterGameLoadingPackageId("taranchuk.fastergameloading")
        );

    [Test]
    public static void CaseDoesNotMatter() =>
        Expect.IsTrue(
            FasterGameLoadingUtils.IsFasterGameLoadingPackageId("Taranchuk.FasterGameLoading")
        );

    [Test]
    public static void OtherModsAreNotMistakenForIt()
    {
        Expect.IsFalse(
            FasterGameLoadingUtils.IsFasterGameLoadingPackageId(
                "taranchuk.performanceoptimizer_steam"
            )
        );
        Expect.IsFalse(
            FasterGameLoadingUtils.IsFasterGameLoadingPackageId("taranchuk.fastergameloading.tests")
        );
    }

    [Test]
    public static void AMissingIdIsNotIt()
    {
        Expect.IsFalse(FasterGameLoadingUtils.IsFasterGameLoadingPackageId(null));
        Expect.IsFalse(FasterGameLoadingUtils.IsFasterGameLoadingPackageId(""));
    }
}
