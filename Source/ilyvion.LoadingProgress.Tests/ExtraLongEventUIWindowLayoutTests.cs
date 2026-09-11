using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class ExtraLongEventUIWindowLayoutTests
{
    [Test]
    public static void ComputeReservedExtentCentersTheBlockOnScreen()
    {
        var rect = ExtraLongEventUIWindowLayout.ComputeReservedExtent(
            400f,
            200f,
            new Vector2(1920f, 1080f)
        );
        Assert.That(rect.x).Is.EqualTo(760f);
        Assert.That(rect.y).Is.EqualTo(440f);
        Assert.That(rect.width).Is.EqualTo(400f);
        Assert.That(rect.height).Is.EqualTo(200f);
    }

    [Test]
    public static void TrimStatusBoxSpaceRemovesTheTopStripReservedForVanillasStatusBox()
    {
        var reserved = new Rect(100f, 200f, 400f, 500f);
        var trimmed = ExtraLongEventUIWindowLayout.TrimStatusBoxSpace(
            reserved,
            statusBoxHeight: 70f
        );
        Assert.That(trimmed.x).Is.EqualTo(100f);
        Assert.That(trimmed.y).Is.EqualTo(287f);
        Assert.That(trimmed.width).Is.EqualTo(400f);
        Assert.That(trimmed.height).Is.EqualTo(413f);
    }

    [Test]
    public static void TrimStatusBoxSpaceNeverGoesNegativeWhenThePaddingExceedsTheHeight()
    {
        var reserved = new Rect(0f, 0f, 100f, 50f);
        var trimmed = ExtraLongEventUIWindowLayout.TrimStatusBoxSpace(
            reserved,
            statusBoxHeight: 70f
        );
        Assert.That(trimmed.height).Is.EqualTo(0f);
    }

    [Test]
    public static void AvoidReservedExtentLeavesPositionAloneWhenThereIsNoReservedExtent()
    {
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(100f, 100f),
            new Vector2(200f, 100f),
            reservedExtent: null,
            screenHeight: 1080f
        );
        Assert.That(position.x).Is.EqualTo(100f);
        Assert.That(position.y).Is.EqualTo(100f);
    }

    [Test]
    public static void AvoidReservedExtentLeavesPositionAloneWhenItDoesNotOverlap()
    {
        // Custom placement can put the window anywhere, including corners far from vanilla's
        // horizontally-centered tip/mod-summary block; those placements shouldn't be nudged.
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(0f, 0f),
            new Vector2(300f, 100f),
            reserved,
            screenHeight: 1080f
        );
        Assert.That(position.x).Is.EqualTo(0f);
        Assert.That(position.y).Is.EqualTo(0f);
    }

    [Test]
    public static void AvoidReservedExtentMovesBelowWhenThatSideIsCloserAndFits()
    {
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(700f, 450f),
            new Vector2(500f, 300f),
            reserved,
            screenHeight: 1080f
        );
        Assert.That(position.x).Is.EqualTo(700f);
        Assert.That(position.y).Is.EqualTo(710f);
    }

    [Test]
    public static void AvoidReservedExtentMovesAboveWhenThatSideIsCloserAndFits()
    {
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(700f, 350f),
            new Vector2(500f, 300f),
            reserved,
            screenHeight: 1080f
        );
        Assert.That(position.x).Is.EqualTo(700f);
        Assert.That(position.y).Is.EqualTo(90f);
    }

    [Test]
    public static void AvoidReservedExtentClampsOnScreenWhenNeitherSideFits()
    {
        var reserved = new Rect(700f, 400f, 500f, 300f);
        var position = ExtraLongEventUIWindowLayout.AvoidReservedExtent(
            new Vector2(700f, 450f),
            new Vector2(500f, 500f),
            reserved,
            screenHeight: 900f
        );
        Assert.That(position.x).Is.EqualTo(700f);
        Assert.That(position.y).Is.EqualTo(0f);
    }

    [Test]
    public static void ComputeMiddleYReturnsTheNaturalCenterWhenThereIsNoReservedExtent()
    {
        var y = ExtraLongEventUIWindowLayout.ComputeMiddleY(
            new Vector2(400f, 200f),
            new Vector2(0f, 0f),
            reservedExtent: null,
            screenHeight: 1000f
        );
        Assert.That(y).Is.EqualTo(400f);
    }

    [Test]
    public static void ComputeMiddleYPlacesTheWindowJustBelowTheBalancedReservedExtent()
    {
        // The reserved extent's own (unbalanced) position is irrelevant here - only its height
        // matters, since ComputeBalancedReservedExtent decides where it actually goes.
        var reserved = new Rect(0f, 999f, 100f, 200f);
        var y = ExtraLongEventUIWindowLayout.ComputeMiddleY(
            new Vector2(400f, 300f),
            new Vector2(0f, 0f),
            reserved,
            screenHeight: 1200f
        );
        Assert.That(y).Is.EqualTo(555f);
    }

    [Test]
    public static void ComputeBalancedReservedExtentCentersTheCombinedGroupOnScreen()
    {
        var avoidanceExtent = new Rect(50f, 0f, 400f, 200f);
        var balanced = ExtraLongEventUIWindowLayout.ComputeBalancedReservedExtent(
            avoidanceExtent,
            ourCombinedHeight: 300f,
            screenHeight: 1200f
        );
        Assert.That(balanced.x).Is.EqualTo(50f);
        Assert.That(balanced.y).Is.EqualTo(345f);
        Assert.That(balanced.width).Is.EqualTo(400f);
        Assert.That(balanced.height).Is.EqualTo(200f);
        // Top margin (above balanced.y) and bottom margin (below our own window, which sits
        // balanced.height + 10px below balanced.y) both come out to 345px - a centered group.
        Assert.That(1200f - (balanced.y + 200f + 10f + 300f)).Is.EqualTo(balanced.y);
    }

    [Test]
    public static void ComputeBlockExtentSpansJustTheMainWindowWhenThereIsNoFasterGameLoadingWindow()
    {
        var (top, bottom) = ExtraLongEventUIWindowLayout.ComputeBlockExtent(
            mainWindowY: 100f,
            mainWindowSize: new Vector2(400f, 150f),
            fasterGameLoadingWindowSize: Vector2.zero,
            fasterGameLoadingGoesAbove: false
        );
        Assert.That(top).Is.EqualTo(100f);
        Assert.That(bottom).Is.EqualTo(250f);
    }

    [Test]
    public static void ComputeBlockExtentIncludesTheFasterGameLoadingWindowBelowWhenItGoesThere()
    {
        var (top, bottom) = ExtraLongEventUIWindowLayout.ComputeBlockExtent(
            mainWindowY: 100f,
            mainWindowSize: new Vector2(400f, 150f),
            fasterGameLoadingWindowSize: new Vector2(400f, 80f),
            fasterGameLoadingGoesAbove: false
        );
        Assert.That(top).Is.EqualTo(100f);
        Assert.That(bottom).Is.EqualTo(340f);
    }

    [Test]
    public static void ComputeBlockExtentIncludesTheFasterGameLoadingWindowAboveWhenItGoesThere()
    {
        var (top, bottom) = ExtraLongEventUIWindowLayout.ComputeBlockExtent(
            mainWindowY: 100f,
            mainWindowSize: new Vector2(400f, 150f),
            fasterGameLoadingWindowSize: new Vector2(400f, 80f),
            fasterGameLoadingGoesAbove: true
        );
        Assert.That(top).Is.EqualTo(10f);
        Assert.That(bottom).Is.EqualTo(250f);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesAboveTheBlockWhenThereIsRoomAndNoReservedExtent()
    {
        var aboveCandidate = new Rect(100f, 190f, 200f, 50f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 250f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: null
        );
        Assert.That(top).Is.EqualTo(190f);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesBelowTheBlockWhenThereIsNoRoomAbove()
    {
        var aboveCandidate = new Rect(100f, -30f, 200f, 50f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 10f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: null
        );
        Assert.That(top).Is.EqualTo(510f);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesBelowTheBlockWhenAboveWouldOverlapTheReservedExtent()
    {
        // Enough vertical room above the block, but that space is where the gameplay tip/mod
        // summary block sits, so the status box needs to go below the main block instead.
        var aboveCandidate = new Rect(100f, 190f, 200f, 50f);
        var reserved = new Rect(0f, 150f, 400f, 300f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 250f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: reserved
        );
        Assert.That(top).Is.EqualTo(510f);
    }

    [Test]
    public static void ComputeStatusBoxTopGoesAboveWhenTheReservedExtentDoesNotOverlapTheCandidate()
    {
        var aboveCandidate = new Rect(100f, 190f, 200f, 50f);
        var reserved = new Rect(700f, 150f, 400f, 300f);
        var top = ExtraLongEventUIWindowLayout.ComputeStatusBoxTop(
            blockTop: 250f,
            blockBottom: 500f,
            aboveCandidate,
            reservedExtent: reserved
        );
        Assert.That(top).Is.EqualTo(190f);
    }
}
