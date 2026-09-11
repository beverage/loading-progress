using System.Xml;
using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class InGameLoadingSessionTests
{
    [Test]
    public static void DetermineKindMapsGeneratingWorldToWorldGeneration()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingWorld",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.WorldGeneration);
    }

    [Test]
    public static void DetermineKindMapsGeneratingPlanetToPlanetRegeneration()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingPlanet",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.PlanetRegeneration);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToNewGameMapGenerationWhenProgramStateIsEntry()
    {
        // The pre-scene-load "GeneratingMap" event (PageUtility.InitGameStart) has no
        // levelToLoad of its own worth relying on here; ProgramState is still Entry at that
        // point.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.NewGameMapGeneration);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToNewGameMapGenerationWhenLevelToLoadIsPlay()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            "Play",
            ProgramState.MapInitializing,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.NewGameMapGeneration);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToNewGameMapGenerationWhenInPlaySceneWithNoMaps()
    {
        // The post-scene-load "GeneratingMap" event (Root_Play.Start -> Game.InitNewGame) has
        // no levelToLoad and ProgramState is already MapInitializing by then; it's recognized
        // as the same kind via the "in the Play scene with no maps yet" condition instead.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.NewGameMapGeneration);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToEncounterMapGenerationWhenInPlayAsyncWithExistingMaps()
    {
        // Settle/SetupCamp/dev gizmos: an in-play "GeneratingMap" event with maps already
        // present, running asynchronously (live progress is possible), must be recognized as
        // its own kind rather than misclassified as new-game map generation.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.EncounterMapGeneration);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToEncounterMapGenerationStaticWhenInPlaySyncWithExistingMaps()
    {
        // Synchronous in-play map generation (gravship landings, the new-colony quest, the dev
        // "generate map here" gizmo) runs in one Update() with no repaint until it finishes, so
        // live progress is impossible and it must not be picked up as EncounterMapGeneration;
        // only the static single-frame kind fits.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.EncounterMapGenerationStatic);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapForNewEncounterToEncounterMapGenerationStatic()
    {
        // Every in-play encounter map (visit site, settlement attack, peace talks, escape ship,
        // transporters, ambush, caravan meeting/demand) queues this key and always runs
        // synchronously; it must be recognized as the static kind regardless of doAsynchronously.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMapForNewEncounter",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.EncounterMapGenerationStatic);
    }

    [Test]
    public static void DetermineKindMapsSpawningColonistsToEncounterMapGeneration()
    {
        // Settle/SetupCamp's second event (CaravanEnterMapUtility.Enter); always recognized as
        // EncounterMapGeneration regardless of context, the same way "GeneratingWorld" always
        // maps to WorldGeneration.
        var kind = InGameLoadingSession.DetermineKind(
            "SpawningColonists",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 2,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.EncounterMapGeneration);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToSaveLoadingWhenLevelToLoadIsPlay()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            "Play",
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.SaveLoading);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToSaveLoadingWhenGameToLoadIsPending()
    {
        // GameDataSaveLoader.LoadGame's scene-load event carries levelToLoad "Play", but the
        // event queued afterwards from Root_Play.Start (which actually reads the save file) has
        // no levelToLoad of its own. It must still be recognized as the same session via
        // GameInitData still holding the save name the first event stashed there.
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.SaveLoading);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToNoneWhenLevelToLoadIsEntry()
    {
        // GenScene.GoToMainMenu queues "LoadingLongEvent" with levelToLoad "Entry"; this must
        // not be picked up as a save-loading session.
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            "Entry",
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToNoneWhenNotInPlaySceneAndNoGameToLoad()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void DetermineKindMapsUnrecognizedKeyToNone()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "Autosaver",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void AdvanceSessionStartsNewSessionWhenAWhitelistedEventBecomesCurrent()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingWorld",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.WorldGeneration);
    }

    [Test]
    public static void AdvanceSessionKeepsActiveKindWhenTheSameEventIsStillRunning()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.WorldGeneration,
            hasCurrentEvent: true,
            eventChanged: false,
            queueEmpty: false,
            resetSignal: false,
            "GenStep - Terrain",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.WorldGeneration);
    }

    [Test]
    public static void AdvanceSessionKeepsActiveKindWhenTheDeferredRedirectEventBecomesCurrent()
    {
        // InGameDeferredActionReplacement's redirected event is a brand new QueuedLongEvent
        // object (eventChanged: true) carrying a key DetermineKind doesn't recognize; it must
        // still continue whatever kind was already active instead of ending the session.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.SaveLoading,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            InGameLoadingSession.DeferredRedirectEventTextKey,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.SaveLoading);
    }

    [Test]
    public static void AdvanceSessionKeepsSessionAliveWhileWaitingBetweenChainedEvents()
    {
        // currentEvent is briefly null (e.g. mid Unity scene-load) but the queue still has the
        // next event of the same chain waiting; the session must not drop.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.NewGameMapGeneration,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.NewGameMapGeneration);
    }

    [Test]
    public static void AdvanceSessionEndsWhenQueueIsEmptyAndNoCurrentEvent()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.SaveLoading,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: true,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void AdvanceSessionEndsImmediatelyOnResetSignalRegardlessOfOtherState()
    {
        // Simulates an error mid-load (ClearQueuedEvents / Scribe.ForceStop); must end the
        // session even though an event is still technically current.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.WorldGeneration,
            hasCurrentEvent: true,
            eventChanged: false,
            queueEmpty: false,
            resetSignal: true,
            "GenerateWorld",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void AdvanceSessionEndsOnReturnToMenuEvent()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.SaveLoading,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            "Entry",
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void AdvanceSessionNewGameMapGenerationSpansTheTwoEventChainAcrossASceneLoad()
    {
        // Full fixture sequence for new-game map generation: PrepForMapGen's "GeneratingMap"
        // event, then a gap while the "Play" scene loads, then Game.InitNewGame's distinct
        // "GeneratingMap" event object, then the session ending once the queue drains.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingMap",
            "Play",
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.NewGameMapGeneration);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.NewGameMapGeneration);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingMap",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.NewGameMapGeneration);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: true,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void AdvanceSessionEncounterMapGenerationSpansTheGeneratingMapAndSpawningColonistsEventPair()
    {
        // Settle: SettleInEmptyTileUtility.Settle queues an async "GeneratingMap" event (reusing
        // MapGenerator.GenerateMap, same as new-game map generation, but with existing maps
        // already present) followed, after that event's worker thread finishes, by a second,
        // distinct "SpawningColonists" QueuedLongEvent object (CaravanEnterMapUtility.Enter);
        // it must be recognized as a continuation of the same session, not an unrelated event.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingMap",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.EncounterMapGeneration);

        // currentEvent is briefly null between the two events (the first event's worker thread
        // has finished but the second hasn't been dequeued yet); the session must not drop.
        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.EncounterMapGeneration);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "SpawningColonists",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 2,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.EncounterMapGeneration);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: true,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 2,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void AdvanceSessionSaveLoadingSpansTheTwoEventChainAcrossASceneLoad()
    {
        // Regression coverage for the bug where loading a save only showed the mod's window for
        // the very first ("Play"-scene-load) event, then silently fell back to vanilla's display
        // for the rest of the load. GameDataSaveLoader.LoadGame's scene-load event ("Play") is
        // followed, after the scene finishes loading, by a second, distinct QueuedLongEvent
        // object (queued from Root_Play.Start) that has the same eventTextKey but no
        // levelToLoad of its own; it must still be recognized as a continuation of the same
        // save-loading session via gameToLoadPending.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            "Play",
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.SaveLoading);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.SaveLoading);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.SaveLoading);

        // The event text changes several times within this same event (world -> map -> init ->
        // spawn) via SetCurrentEventText, but the event object itself never changes, so the
        // session must keep its kind with no reclassification.
        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: false,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.SaveLoading);

        // The final, untextkeyed screen-fade event ends the session.
        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Assert.That(kind).Is.EqualTo(InGameSessionKind.None);
    }

    [Test]
    public static void DeterminePhaseFromLabelAdvancesNewGameMapGenerationPhasesInOrder()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            InGameSessionPhase.NewGameMapGeneration_SetUp,
            "Generate contents into map"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.NewGameMapGeneration_GenSteps);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            phase,
            "Finalize map init"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.NewGameMapGeneration_Finalize);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            phase,
            "MapComponent.MapGenerated()"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.NewGameMapGeneration_PostInit);
    }

    [Test]
    public static void DeterminePhaseFromLabelAlsoAdvancesToPostInitOnMapGeneratorPostInitLabel()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            InGameSessionPhase.NewGameMapGeneration_Finalize,
            "Map generator post init"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.NewGameMapGeneration_PostInit);
    }

    [Test]
    public static void DeterminePhaseFromLabelKeepsNewGameMapGenerationPhaseForUnrelatedLabels()
    {
        // The per-step "GenStep - <def>" labels advance the inner progress bar (via
        // OnProfilerLabel), not the outer phase; the phase only changes on the boundary labels.
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            InGameSessionPhase.NewGameMapGeneration_GenSteps,
            "GenStep - ElevationFertility"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.NewGameMapGeneration_GenSteps);
    }

    [Test]
    public static void DeterminePhaseFromLabelAdvancesEncounterMapGenerationPhasesInOrder()
    {
        // Settle/SetupCamp reuse MapGenerator.GenerateMap, so the label sequence is identical to
        // new-game map generation; only the target phase enum differs.
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.EncounterMapGeneration,
            InGameSessionPhase.EncounterMapGeneration_SetUp,
            "Generate contents into map"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.EncounterMapGeneration_GenSteps);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.EncounterMapGeneration,
            phase,
            "Finalize map init"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.EncounterMapGeneration_Finalize);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.EncounterMapGeneration,
            phase,
            "MapComponent.MapGenerated()"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.EncounterMapGeneration_PostInit);
    }

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsTrueWhenTheSpawningColonistsEventBecomesCurrent() =>
        Assert
            .That(
                InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                    InGameSessionKind.EncounterMapGeneration,
                    eventChanged: true,
                    "SpawningColonists"
                )
            )
            .Is.True();

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsFalseForOtherKinds() =>
        // NewGameMapGeneration never chains into a "SpawningColonists" event; a modded call
        // site emitting one while a new-game session is active must not be picked up.
        Assert
            .That(
                InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                    InGameSessionKind.NewGameMapGeneration,
                    eventChanged: true,
                    "SpawningColonists"
                )
            )
            .Is.False();

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsFalseWhenTheEventDidNotChange() =>
        // Guards against re-entering the phase (and resetting its progress) on every frame the
        // SpawningColonists event stays current, not just the one frame it becomes current.
        Assert
            .That(
                InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                    InGameSessionKind.EncounterMapGeneration,
                    eventChanged: false,
                    "SpawningColonists"
                )
            )
            .Is.False();

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsFalseForUnrelatedKeys() =>
        Assert
            .That(
                InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                    InGameSessionKind.EncounterMapGeneration,
                    eventChanged: true,
                    "GeneratingMap"
                )
            )
            .Is.False();

    [Test]
    public static void DeterminePhaseFromLabelAdvancesSaveLoadingToFinishingOnGameFinalizeInitLabel()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.SaveLoading,
            InGameSessionPhase.SaveLoading_Spawning,
            "Game.FinalizeInit"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.SaveLoading_Finishing);
    }

    [Test]
    public static void DeterminePhaseFromLabelKeepsSaveLoadingPhaseForUnrelatedLabels()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.SaveLoading,
            InGameSessionPhase.SaveLoading_Spawning,
            "Spawn everything into the map"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.SaveLoading_Spawning);
    }

    [Test]
    public static void DeterminePhaseFromLabelIsANoOpForWorldGeneration()
    {
        // World generation's only phase transition (SetupSteps -> LayerSteps) comes from the
        // GeneratePlanetLayer prefix, which has the layer itself available; no label alone can
        // drive it, since the "WorldGen - <type name>" label isn't even human-readable.
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.WorldGeneration,
            InGameSessionPhase.WorldGeneration_SetupSteps,
            "WorldGenStep - Tiles"
        );
        Assert.That(phase).Is.EqualTo(InGameSessionPhase.WorldGeneration_SetupSteps);
    }

    [Test]
    public static void DeterminePhaseFromLabelIsANoOpForPlanetRegeneration() =>
        // Planet regeneration has only the one phase and emits no DeepProfiler labels at all
        // (per-layer progress instead comes from the WorldDrawLayerBase.Regenerate prefix).
        Assert
            .That(
                InGameLoadingSession.DeterminePhaseFromLabel(
                    InGameSessionKind.PlanetRegeneration,
                    InGameSessionPhase.PlanetRegeneration_RegeneratingLayers,
                    "GenStep - Terrain"
                )
            )
            .Is.EqualTo(InGameSessionPhase.PlanetRegeneration_RegeneratingLayers);

    [Test]
    public static void DeterminePhaseFromLabelIsANoOpForEncounterMapGenerationStatic() =>
        // The static kind's one painted frame is fixed before the synchronous action (and any
        // labels it would emit) even runs, so it has only the one phase and ignores labels.
        Assert
            .That(
                InGameLoadingSession.DeterminePhaseFromLabel(
                    InGameSessionKind.EncounterMapGenerationStatic,
                    InGameSessionPhase.EncounterMapGenerationStatic_Generating,
                    "GenStep - ElevationFertility"
                )
            )
            .Is.EqualTo(InGameSessionPhase.EncounterMapGenerationStatic_Generating);

    [Test]
    public static void DetermineSaveLoadingPhaseFromEventTextCallOrderMapsCallsInFixedOrder()
    {
        Assert
            .That(InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(1))
            .Is.EqualTo(InGameSessionPhase.SaveLoading_World);
        Assert
            .That(InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(2))
            .Is.EqualTo(InGameSessionPhase.SaveLoading_Maps);
        Assert
            .That(InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(3))
            .Is.EqualTo(InGameSessionPhase.SaveLoading_Initializing);
        Assert
            .That(InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(4))
            .Is.EqualTo(InGameSessionPhase.SaveLoading_Spawning);
    }

    [Test]
    public static void DetermineSaveLoadingPhaseFromEventTextCallOrderClampsCallsPastTheFourth() =>
        // Defensive: SetCurrentEventText is only ever called 4 times by vanilla, but a modded
        // caller (or a future game version) calling it a 5th time shouldn't invent a new phase.
        Assert
            .That(InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(5))
            .Is.EqualTo(InGameSessionPhase.SaveLoading_Spawning);

    [Test]
    public static void DetermineSaveLoadingPhaseFromEventTextCallOrderDefaultsToReadingFile() =>
        Assert
            .That(InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(0))
            .Is.EqualTo(InGameSessionPhase.SaveLoading_ReadingFile);

    [Test]
    public static void IsSaveLoadingSubProgressResetLabelIsTrueForBothInitializingLoopLabels()
    {
        Assert
            .That(
                InGameLoadingSession.IsSaveLoadingSubProgressResetLabel(
                    "ResolveAllCrossReferences()"
                )
            )
            .Is.True();
        Assert
            .That(InGameLoadingSession.IsSaveLoadingSubProgressResetLabel("DoAllPostLoadInits()"))
            .Is.True();
    }

    [Test]
    public static void IsSaveLoadingSubProgressResetLabelIsTrueForThingPostMapInit() =>
        Assert
            .That(InGameLoadingSession.IsSaveLoadingSubProgressResetLabel("Thing.PostMapInit()"))
            .Is.True();

    [Test]
    public static void IsSaveLoadingSubProgressResetLabelIsFalseForUnrelatedLabels() =>
        Assert
            .That(
                InGameLoadingSession.IsSaveLoadingSubProgressResetLabel(
                    "Spawn everything into the map"
                )
            )
            .Is.False();

    [Test]
    public static void DetermineInitializingSubPhaseTotalUsesCrossReferencingExposablesCountForResolveAllCrossReferences() =>
        Assert
            .That(
                InGameLoadingSession.DetermineInitializingSubPhaseTotal(
                    "ResolveAllCrossReferences()",
                    crossReferencingExposablesCount: 42,
                    saveablesToPostLoadCount: 7
                )
            )
            .Is.EqualTo(42);

    [Test]
    public static void DetermineInitializingSubPhaseTotalUsesSaveablesToPostLoadCountForDoAllPostLoadInits() =>
        Assert
            .That(
                InGameLoadingSession.DetermineInitializingSubPhaseTotal(
                    "DoAllPostLoadInits()",
                    crossReferencingExposablesCount: 42,
                    saveablesToPostLoadCount: 7
                )
            )
            .Is.EqualTo(7);

    [Test]
    public static void DetermineInitializingSubPhaseTotalIsZeroForThingPostMapInit() =>
        // The Thing.PostMapInit() loop's total isn't known upfront from any live collection count;
        // it's set later, from the first postfix call that has a Map instance to read
        // map.listerThings.AllThings.Count from.
        Assert
            .That(
                InGameLoadingSession.DetermineInitializingSubPhaseTotal(
                    "Thing.PostMapInit()",
                    crossReferencingExposablesCount: 42,
                    saveablesToPostLoadCount: 7
                )
            )
            .Is.EqualTo(0);

    [Test]
    public static void DetermineThingPostMapInitPhaseMapsMapGenerationKindsToTheirOwnFinalizePhase()
    {
        Assert
            .That(
                InGameLoadingSession.DetermineThingPostMapInitPhase(
                    InGameSessionKind.NewGameMapGeneration
                )
            )
            .Is.EqualTo(InGameSessionPhase.NewGameMapGeneration_Finalize);
        Assert
            .That(
                InGameLoadingSession.DetermineThingPostMapInitPhase(
                    InGameSessionKind.EncounterMapGeneration
                )
            )
            .Is.EqualTo(InGameSessionPhase.EncounterMapGeneration_Finalize);
    }

    [Test]
    public static void DetermineThingPostMapInitPhaseMapsSaveLoadingToItsSpawningPhase() =>
        // SaveLoading has no dedicated Finalize phase: Map.FinalizeInit() (and so its
        // Thing.PostMapInit() loop) runs inside Spawning instead.
        Assert
            .That(
                InGameLoadingSession.DetermineThingPostMapInitPhase(InGameSessionKind.SaveLoading)
            )
            .Is.EqualTo(InGameSessionPhase.SaveLoading_Spawning);

    [Test]
    public static void DetermineThingPostMapInitPhaseIsNullForKindsThatNeverReachIt()
    {
        Assert
            .That(
                InGameLoadingSession.DetermineThingPostMapInitPhase(
                    InGameSessionKind.WorldGeneration
                )
            )
            .Is.Null();
        Assert
            .That(
                InGameLoadingSession.DetermineThingPostMapInitPhase(
                    InGameSessionKind.PlanetRegeneration
                )
            )
            .Is.Null();
        Assert
            .That(
                InGameLoadingSession.DetermineThingPostMapInitPhase(
                    InGameSessionKind.EncounterMapGenerationStatic
                )
            )
            .Is.Null();
        Assert
            .That(InGameLoadingSession.DetermineThingPostMapInitPhase(InGameSessionKind.None))
            .Is.Null();
    }

    [Test]
    public static void DetermineDeferredPhaseMapsEachChunkableKindToItsOwnDeferredPhase()
    {
        Assert
            .That(InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.WorldGeneration))
            .Is.EqualTo(InGameSessionPhase.WorldGeneration_Deferred);
        Assert
            .That(
                InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.NewGameMapGeneration)
            )
            .Is.EqualTo(InGameSessionPhase.NewGameMapGeneration_Deferred);
        Assert
            .That(InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.SaveLoading))
            .Is.EqualTo(InGameSessionPhase.SaveLoading_Deferred);
        Assert
            .That(
                InGameLoadingSession.DetermineDeferredPhase(
                    InGameSessionKind.EncounterMapGeneration
                )
            )
            .Is.EqualTo(InGameSessionPhase.EncounterMapGeneration_Deferred);
    }

    [Test]
    public static void DetermineDeferredPhaseIsNullForKindsInGameDeferredActionReplacementNeverChunks()
    {
        // PlanetRegeneration already gets live per-layer progress from vanilla's own
        // enumerator-based long event, and EncounterMapGenerationStatic's one painted frame
        // precedes any of the generation it describes; neither goes through
        // InGameDeferredActionReplacement.
        Assert
            .That(InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.PlanetRegeneration))
            .Is.Null();
        Assert
            .That(
                InGameLoadingSession.DetermineDeferredPhase(
                    InGameSessionKind.EncounterMapGenerationStatic
                )
            )
            .Is.Null();
        Assert.That(InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.None)).Is.Null();
    }

    [Test]
    public static void AdvanceProgressCurrentIncrementsBelowMax() =>
        Assert.That(InGameLoadingSession.AdvanceProgressCurrent(3, 5)).Is.EqualTo(4);

    // Regression coverage for §8 risk item 8: counts derived from approximate totals (e.g.
    // things spawned after load) must never let the inner bar exceed its own max.
    [Test]
    public static void AdvanceProgressCurrentClampsAtMax() =>
        Assert.That(InGameLoadingSession.AdvanceProgressCurrent(5, 5)).Is.EqualTo(5);

    [Test]
    public static void CountDirtyVisibleLayersCountsOnlyLayersThatAreBothDirtyAndVisible()
    {
        var count = InGameLoadingSession.CountDirtyVisibleLayers([
            (Dirty: true, Visible: true),
            (Dirty: true, Visible: false),
            (Dirty: false, Visible: true),
            (Dirty: false, Visible: false),
            (Dirty: true, Visible: true),
        ]);
        Assert.That(count).Is.EqualTo(2);
    }

    [Test]
    public static void CountDirtyVisibleLayersReturnsZeroForAnEmptyLayerList() =>
        Assert.That(InGameLoadingSession.CountDirtyVisibleLayers([])).Is.EqualTo(0);

    [Test]
    public static void StripKnownLabelPrefixStripsGenStepPrefix() =>
        Assert
            .That(InGameLoadingSession.StripKnownLabelPrefix("GenStep - ElevationFertility"))
            .Is.EqualTo("ElevationFertility");

    [Test]
    public static void StripKnownLabelPrefixStripsWorldGenStepPrefix() =>
        Assert
            .That(InGameLoadingSession.StripKnownLabelPrefix("WorldGenStep - Tiles"))
            .Is.EqualTo("Tiles");

    [Test]
    public static void StripKnownLabelPrefixLeavesOtherLabelsUnchanged() =>
        Assert
            .That(InGameLoadingSession.StripKnownLabelPrefix("Finalize map init"))
            .Is.EqualTo("Finalize map init");

    [Test]
    public static void IsSuppressedWorldGenLayerLabelDetectsTheLayerBoundaryLabel() =>
        Assert
            .That(
                InGameLoadingSession.IsSuppressedWorldGenLayerLabel(
                    "WorldGen - RimWorld.Planet.SurfaceLayer"
                )
            )
            .Is.True();

    // "WorldGenStep - Tiles" must not be mistaken for the "WorldGen - <type>" layer-boundary
    // label just because it shares a prefix; it's a legitimate, readable label on its own.
    [Test]
    public static void IsSuppressedWorldGenLayerLabelDoesNotMatchWorldGenStepLabels() =>
        Assert
            .That(InGameLoadingSession.IsSuppressedWorldGenLayerLabel("WorldGenStep - Tiles"))
            .Is.False();

    [Test]
    public static void CountThingsAcrossMapsSumsThingNodesFromEveryMap()
    {
        var doc = ParseXmlFixture(
            """
            <game>
                <maps>
                    <li>
                        <things>
                            <thing Class="Plant" />
                            <thing Class="Mineable" />
                        </things>
                    </li>
                    <li>
                        <things>
                            <thing Class="Building" />
                        </things>
                    </li>
                </maps>
            </game>
            """
        );

        Assert.That(InGameLoadingSession.CountThingsAcrossMaps(doc.DocumentElement)).Is.EqualTo(3);
    }

    [Test]
    public static void CountThingsAcrossMapsReturnsZeroWhenThereIsNoMapsNode()
    {
        var doc = ParseXmlFixture("<game></game>");

        Assert.That(InGameLoadingSession.CountThingsAcrossMaps(doc.DocumentElement)).Is.EqualTo(0);
    }

    [Test]
    public static void CountThingsAcrossMapsIgnoresMapsWithNoThingsNode()
    {
        var doc = ParseXmlFixture(
            """
            <game>
                <maps>
                    <li></li>
                    <li>
                        <things>
                            <thing Class="Building" />
                        </things>
                    </li>
                </maps>
            </game>
            """
        );

        Assert.That(InGameLoadingSession.CountThingsAcrossMaps(doc.DocumentElement)).Is.EqualTo(1);
    }

    // XmlDocument.LoadXml(string) resolves external entities by default (XXE risk); these
    // fixtures are hardcoded test data, not untrusted input, but XmlReader with DtdProcessing
    // disabled avoids relying on that distinction.
    private static XmlDocument ParseXmlFixture(string xml)
    {
        using var reader = XmlReader.Create(
            new StringReader(xml),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
        );
        var doc = new XmlDocument();
        doc.Load(reader);
        return doc;
    }
}
