namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Follows the startup's tail: the long events that run once loading is over, up to the first
/// frame the main menu sits idle. Keeps the loading window's activity line on the event that
/// is running, ends the startup for the window when the menu is reached and, with tracking
/// on, times each event under the mod whose code it runs.
/// </summary>
/// <remarks>
/// <para>
/// Loading is over, and the tracking clock stops, where the interface begins initializing,
/// the window's Finished stage. What runs after, the rest of the interface's initialization
/// and the windows and setup other mods queue for after loading, is time the player waits
/// through, and until now nothing measured it and the window had already left the screen.
/// Each such event is timed from the frame it became the current one to the frame it stopped
/// being it, and credited to the mod whose code it runs, under its own category, so it shows
/// beside everything else that mod cost. The interface's own event is timed from the clock
/// stop instead, since it finishes within the frame the clock stops in.
/// </para>
/// <para>
/// Time the game spends paused is left out. The engine keeps running in the background only
/// while it loads; on the first idle frame it applies the player's 'Run in background'
/// preference, off by default, and from then on an unfocused game stops between frames. A
/// player who switched to another window during a long load would otherwise find the time
/// away counted as loading time.
/// </para>
/// </remarks>
internal static class PostLoadTracker
{
    internal const string Category = "LoadingProgress.StartupImpact.PostLoadLongEvent";

    // Two idle frames closer together than this mean the menu is drawing freely; a wait
    // between frames longer than this, while the game was in the background, was a pause.
    private const float QuickFrameMs = 250f;

    // A menu that never draws two frames that close together, below four frames a second,
    // counts as reached at this many idle frames in a row.
    private const int SlowMenuIdleFrames = 5;

    private static bool _done;
    private static LongEventHandler.QueuedLongEvent? _current;
    private static ModContentPack? _currentOwner;
    private static string? _currentCategory;

    private static float _lastIdleFrameMs = -1f;
    private static int _idleFrames;

    private static bool _watchingFrames;
    private static float _frameEndMs = -1f;
    private static float _frameStartMs = -1f;
    private static bool _unfocusedSinceFrameEnd;
    private static float _pausedMs;

    private static float RealtimeMs => Time.realtimeSinceStartup * 1000f;

    internal static void Update()
    {
        if (_done)
        {
            return;
        }

        // Timing needs tracking; the window's tail runs with or without it.
        var startupImpact = LoadingProgressMod.instance?.StartupImpact;
        var timing =
            startupImpact is { WasTrackingEnabledAtStartup: true, LoadingTimeMeasured: true };
        var finished = LoadingProgressWindow.CurrentStage == LoadingStage.Finished;
        if (!timing && !finished)
        {
            return;
        }

        WatchFrames();
        var now = RealtimeMs;
        var paused = PauseBeforeThisFrame(
            _frameEndMs,
            _frameStartMs,
            now,
            _unfocusedSinceFrameEnd,
            Application.runInBackground
        );
        _pausedMs += paused;

        var current = LongEventHandler.currentEvent;
        if (timing && (!ReferenceEquals(current, _current) || paused > 0f))
        {
            // An event that was current through a pause has the pause taken back off its
            // time, and goes on being timed from here.
            StopCurrent(paused);
            if (current != null)
            {
                StartCurrent(current);
            }
        }
        if (finished && current != null)
        {
            LoadingProgressWindow.ShowPostLoadEvent(current);
        }

        if (Current.ProgramState != ProgramState.Entry)
        {
            // Straight into a game (a quicktest, say): there is no idle menu to wait for.
            Finish(startupImpact, menuReached: false);
            return;
        }

        if (
            finished
            && current == null
            && !LongEventHandler.AnyEventNowOrWaiting
            && Find.UIRoot != null
        )
        {
            // The queue going empty is not the player being able to click. A mod that builds
            // its state on the menu's first frame stalls the main thread between that frame
            // and the next, so the menu counts as reached only once two idle frames have come
            // close together, which includes any such stall in the time to the menu. Pauses
            // are left out of the comparison, so one is not mistaken for a stall.
            var active = now - _pausedMs;
            _idleFrames++;
            var settled = IsMenuSettled(_lastIdleFrameMs, active, _idleFrames);
            _lastIdleFrameMs = active;
            if (settled)
            {
                Finish(startupImpact, menuReached: true);
            }
        }
        else
        {
            _lastIdleFrameMs = -1f;
            _idleFrames = 0;
        }
    }

    /// <summary>
    /// Notes when this frame's long-event work begins. A pause in the background ends there,
    /// and what the frame goes on to run, such as a synchronous event that takes seconds, is
    /// the startup's own time.
    /// </summary>
    internal static void MarkFrameStart()
    {
        if (!_done)
        {
            _frameStartMs = RealtimeMs;
        }
    }

    /// <summary>
    /// Starts timing the long event running when the clock stops: the interface's own
    /// initialization. The clock stops at a profiler label inside that event, and the event
    /// finishes, with the deferred tasks it queues, within the same frame, so Update would
    /// never see it as current.
    /// </summary>
    internal static void StartAtClockStop()
    {
        if (!_done && _current == null && LongEventHandler.currentEvent is { } current)
        {
            StartCurrent(current);
        }
    }

    /// <summary>
    /// Runs Loading Progress's own work during the tail, such as saving its report, timed
    /// under Loading Progress rather than under whichever event it runs inside.
    /// </summary>
    internal static void RunAsOwnWork(string description, Action work)
    {
        TimeOnThisThread();
        var resume = _current;
        StopCurrent(0f);
        var mod = LoadingProgressMod.instance.Content;
        var category = $"{Category}|{description}";
        StartupImpactProfilerUtil.StartModProfiler(mod, category);
        try
        {
            work();
        }
        finally
        {
            StartupImpactProfilerUtil.StopModProfiler(mod, category);
            if (resume != null && !_done)
            {
                StartCurrent(resume);
            }
        }
    }

    /// <summary>
    /// Ends the tail: the window records its loading time and leaves, and the tracker, when
    /// the menu was reached, takes its time to it.
    /// </summary>
    private static void Finish(StartupImpact? startupImpact, bool menuReached)
    {
        StopCurrent(0f);
        _done = true;
        if (_watchingFrames)
        {
            Application.focusChanged -= OnFocusChanged;
        }
        LoadingProgressWindow.CompleteStartup(_pausedMs);
        if (menuReached)
        {
            startupImpact?.MarkMenuReached(_pausedMs);
        }
    }

    /// <summary>
    /// Whether an idle frame at <paramref name="nowMs"/> counts the menu as usable: the first
    /// idle frame never does (there is nothing before it), and one that comes after a stall
    /// does not either, since the stall is what the player was waiting through. A menu that
    /// never draws quickly counts as reached after <see cref="SlowMenuIdleFrames"/> idle frames
    /// in a row.
    /// </summary>
    internal static bool IsMenuSettled(float lastIdleFrameMs, float nowMs, int idleFrames) =>
        lastIdleFrameMs >= 0f
        && (nowMs - lastIdleFrameMs < QuickFrameMs || idleFrames >= SlowMenuIdleFrames);

    /// <summary>
    /// The pause before this frame: the wait from the end of the last frame to
    /// <paramref name="frameStartMs"/>, where this frame's long events begin, or to
    /// <paramref name="nowMs"/> when no frame start was recorded. What the frame's long events
    /// then run, such as a synchronous event that takes seconds, is not part of it.
    /// </summary>
    internal static float PauseBeforeThisFrame(
        float frameEndMs,
        float frameStartMs,
        float nowMs,
        bool unfocusedSinceFrameEnd,
        bool runInBackground
    ) =>
        PauseIn(
            frameEndMs,
            frameStartMs >= 0f ? frameStartMs : nowMs,
            unfocusedSinceFrameEnd,
            runInBackground
        );

    /// <summary>
    /// How much of the wait from the end of the last frame to <paramref name="frameStartMs"/>,
    /// where this frame's long-event work begins, the game sat paused: all of it when the wait
    /// was longer than a frame, the game was in the background at some point since that frame
    /// ended, and it does not run there; else none.
    /// </summary>
    internal static float PauseIn(
        float frameEndMs,
        float frameStartMs,
        bool unfocusedSinceFrameEnd,
        bool runInBackground
    )
    {
        if (frameEndMs < 0f || !unfocusedSinceFrameEnd || runInBackground)
        {
            return 0f;
        }

        var waitMs = frameStartMs - frameEndMs;
        return waitMs > QuickFrameMs ? waitMs : 0f;
    }

    private static void WatchFrames()
    {
        if (_watchingFrames || Find.Root == null)
        {
            return;
        }

        _watchingFrames = true;
        Application.focusChanged += OnFocusChanged;
        _ = Find.Root.StartCoroutine(FrameEnds());
    }

    private static void OnFocusChanged(bool focused)
    {
        if (!focused)
        {
            _unfocusedSinceFrameEnd = true;
        }
    }

    // When each frame ends, after everything it drew: a pause falls between one frame's end
    // and the next frame's start.
    private static IEnumerator FrameEnds()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (!_done)
        {
            yield return endOfFrame;
            _frameEndMs = RealtimeMs;
            _unfocusedSinceFrameEnd = !Application.isFocused;
        }
    }

    // Everything after loading runs on the main thread, and only the active thread's timings
    // count toward the totals and the stage ledger, or can have a pause taken off. The
    // deferred-task replacement makes the main thread the active one, but that patch is
    // skipped when the settings turn the initialization patches off, so the tracker does it
    // too before it times anything.
    private static void TimeOnThisThread() =>
        LoadingProgressMod.instance.StartupImpact.UpdateActiveThreadId();

    private static void StartCurrent(LongEventHandler.QueuedLongEvent queuedEvent)
    {
        _current = queuedEvent;
        _currentCategory = $"{Category}|{Describe(queuedEvent)}";
        _currentOwner = OwnerOf(queuedEvent);
        StartTiming(_currentOwner, _currentCategory);
    }

    /// <summary>
    /// Starts <paramref name="category"/> under <paramref name="owner"/>, or under the base
    /// game when it is null, making this thread, the main one, the active thread first.
    /// </summary>
    internal static void StartTiming(ModContentPack? owner, string category)
    {
        TimeOnThisThread();
        if (owner == null)
        {
            StartupImpactProfilerUtil.StartBaseGameProfiler(category);
        }
        else
        {
            StartupImpactProfilerUtil.StartModProfiler(owner, category);
        }
    }

    // Stops timing the current event, taking discountMs, time the game sat paused, back off.
    private static void StopCurrent(float discountMs)
    {
        if (_current == null || _currentCategory == null)
        {
            return;
        }

        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        if (_currentOwner == null)
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler(_currentCategory);
            startupImpact.BaseGameProfiler.Discount(_currentCategory, discountMs);
        }
        else
        {
            StartupImpactProfilerUtil.StopModProfiler(_currentOwner, _currentCategory);
            startupImpact
                .Modlist.GetModInfoFor(_currentOwner)
                ?.Profiler.Discount(_currentCategory, discountMs);
        }

        _current = null;
        _currentOwner = null;
        _currentCategory = null;
    }

    internal static ModContentPack? OwnerOf(LongEventHandler.QueuedLongEvent queuedEvent) =>
        OwnerOf(queuedEvent.eventAction, queuedEvent.eventActionEnumerator);

    /// <summary>
    /// The mod whose code an event runs, or null for the base game.
    /// </summary>
    internal static ModContentPack? OwnerOf(Delegate? action, object? enumerator)
    {
        var assembly = action?.Method.DeclaringType?.Assembly ?? enumerator?.GetType().Assembly;
        return assembly == null ? null : Utilities.FindModByAssembly(assembly);
    }

    internal static string Describe(LongEventHandler.QueuedLongEvent queuedEvent) =>
        Describe(
            queuedEvent.eventTextKey,
            queuedEvent.eventAction,
            queuedEvent.eventActionEnumerator
        );

    /// <summary>
    /// What to call an event: its text as the player saw it when the key translates, else the
    /// key, else the method it ran, named the way it was written.
    /// </summary>
    internal static string Describe(string? key, Delegate? action, object? enumerator)
    {
        if (key is { Length: > 0 } text)
        {
            return text.CanTranslate() ? text.Translate().ToString() : text;
        }

        if (action != null)
        {
            return DescribeCode(action.Method.DeclaringType, action.Method.Name);
        }

        var enumeratorType = enumerator?.GetType();
        return enumeratorType != null
            ? DescribeCode(enumeratorType.DeclaringType, enumeratorType.Name)
            : "?";
    }

    /// <summary>
    /// A method named the way it was written. A lambda compiles to a method named like
    /// <c>&lt;Init&gt;b__3_0</c> on a class nested in the type it was written in, and an
    /// iterator to a class named like <c>&lt;Load&gt;d__5</c>, neither of which tells a
    /// player anything; this gives the written type's full name and the method in brackets.
    /// </summary>
    internal static string DescribeCode(Type? type, string memberName)
    {
        while (type?.DeclaringType != null && IsCompilerGenerated(type))
        {
            type = type.DeclaringType;
        }

        var name = WrittenName(memberName);
        return type == null ? name
            : name.StartsWith('.') ? $"{type.FullName}{name}"
            : $"{type.FullName}.{name}";
    }

    private static bool IsCompilerGenerated(Type type) =>
        type.Name.StartsWith('<') || type.IsDefined(typeof(CompilerGeneratedAttribute), false);

    private static string WrittenName(string name)
    {
        if (name.StartsWith('<'))
        {
            var close = name.IndexOf('>', StringComparison.Ordinal);
            if (close > 1)
            {
                return name[1..close];
            }
        }
        return name;
    }
}
