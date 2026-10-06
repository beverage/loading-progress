namespace ilyvion.LoadingProgress.StartupImpact;

internal abstract class SingleThreadedProfiler(string measurementTarget)
{
    private readonly object _modificationLock = new();
    private readonly List<string> _categories = [];

    public float Total { get; private set; }

    public abstract void Start();
    public abstract float Stop();

    public virtual float StopAndStart()
    {
        var res = Stop();
        Start();
        return res;
    }

    private string ProfilerName
    {
        get => field == null ? "" : $"{field} profiler";
    } = measurementTarget;

    public void Start(string category) => _ = Start(category, out _);

    /// <summary>
    /// Starts timing <paramref name="category"/>, inside whatever category is open.
    /// </summary>
    /// <returns>
    /// The milliseconds the open category ran since it last started, which are that
    /// category's, with its name in <paramref name="interrupted"/>; 0 and null when no
    /// category was open.
    /// </returns>
    public float Start(string category, out string? interrupted)
    {
        interrupted = null;
        if (string.IsNullOrEmpty(category))
        {
            return 0f;
        }

        lock (_modificationLock)
        {
            var ms = 0f;
            if (_categories.Count > 0)
            {
                ms = StopAndStart();
                Total += ms;
                interrupted = _categories[0];
            }
            else
            {
                Start();
            }

            _categories.Insert(0, category);
            return ms;
        }
    }

    public float Stop(string category) => Stop(category, out var _);

    public float Stop(string category, out string actualCategory)
    {
        lock (_modificationLock)
        {
            if (_categories.Count == 0)
            {
                if (category != null)
                {
                    Log.Error(
                        "Stopping "
                            + ProfilerName
                            + " for ["
                            + category
                            + "] while it's already inactive. Current categories: ["
                            + string.Join(", ", _categories)
                            + "]"
                    );
                }

                actualCategory = "none";
                return 0;
            }

            if (category != null && category != _categories[0])
            {
                Log.Error(
                    $"Stopping {ProfilerName} for [expected: {category}] but currently timing [actual: {_categories[0]}]"
                );
            }

            actualCategory = _categories[0];
            _categories.RemoveAt(0);

            var ms = _categories.Count > 0 ? StopAndStart() : Stop();
            Total += ms;
            return ms;
        }
    }
}
