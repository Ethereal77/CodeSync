using System.Diagnostics;

namespace CodeSync.Core;

/// <summary>
///   Provides a mechanism to report progress at controlled intervals to avoid excessive updates.
/// </summary>
internal sealed class ThrottledReporter
{
    private readonly Lock _gate = new();

    private readonly IProgress<ScanProgress>? _progress;

    private readonly int _total;
    private readonly int _candidateCount;
    private readonly int _ignored;

    private readonly TimeSpan _progressInterval;
    private readonly long _startedTimestamp;
    private long _lastReportTimestamp;  // Tracks the timestamp of the last progress report
    private int _lastReportedCompleted = -1;  // Tracks the last completed count to avoid redundant reports


    /// <summary>
    ///   Initializes a new instance of the <see cref="ThrottledReporter"/> class.
    /// </summary>
    /// <param name="progress">The progress reporter to report updates to.</param>
    /// <param name="progressInterval">The minimum interval between progress reports.</param>
    /// <param name="total">The total number of items to process.</param>
    /// <param name="candidateCount">The number of candidate items to process.</param>
    /// <param name="ignored">The number of ignored items.</param>
    public ThrottledReporter(IProgress<ScanProgress>? progress, TimeSpan progressInterval, int total, int candidateCount, int ignored)
    {
        _progress = progress;
        _total = total;
        _candidateCount = candidateCount;
        _ignored = ignored;
        _progressInterval = progressInterval;
        _startedTimestamp = Stopwatch.GetTimestamp();
        _lastReportTimestamp = Stopwatch.GetTimestamp();
    }


    /// <summary>
    ///   Reports the progress of the operation, throttled to avoid excessive updates.
    /// </summary>
    /// <param name="completed">The number of items that have been completed so far.</param>
    /// <param name="force">Whether to force the progress report regardless of the throttling interval.</param>
    public void Report(int completed, bool force = false)
    {
        if (_progress is null)
            return;

        long now;
        var elapsed = Stopwatch.GetElapsedTime(_lastReportTimestamp);

        // Skip reporting if not forced, not completed, and within the progress interval
        if (!force && completed < _candidateCount && elapsed < _progressInterval)
            return;

        lock (_gate)
        {
            now = Stopwatch.GetTimestamp();
            elapsed = Stopwatch.GetElapsedTime(_lastReportTimestamp);

            // Skip reporting if not forced, not completed, and within the progress interval (double-check inside the lock)
            if (!force && completed < _candidateCount && elapsed < _progressInterval)
                return;

            // Skip reporting if the completed count hasn't changed since the last report
            if (force && completed == _lastReportedCompleted)
                return;

            _lastReportTimestamp = now;
            _lastReportedCompleted = completed;

            _progress.Report(new ScanProgress(ScanPhase.Hashing, _total, completed, _ignored,
                                              Stopwatch.GetElapsedTime(_startedTimestamp)));
        }
    }
}
