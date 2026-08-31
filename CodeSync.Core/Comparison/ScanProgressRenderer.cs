namespace CodeSync.Core;

/// <summary>
///   Renders scan progress messages to a <see cref="TextWriter"/>.
/// </summary>
internal sealed class ScanProgressRenderer : IProgress<ScanProgress>
{
    private readonly Lock _gate = new();

    private readonly TextWriter _writer;
    private readonly bool _interactive;

    private bool _dynamicLineActive;
    private int _lastRenderedLength;


    /// <summary>
    ///   Initializes a new instance of the <see cref="ScanProgressRenderer"/> class.
    /// </summary>
    /// <param name="writer">The destination for rendered progress messages.</param>
    /// <param name="interactive">A value indicating whether the destination supports in-place line updates.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="writer"/> is <see langword="null"/>.</exception>
    public ScanProgressRenderer(TextWriter writer, bool interactive)
    {
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
        _interactive = interactive;
    }


    /// <summary>
    ///   Renders a scan progress update.
    /// </summary>
    /// <param name="scanProgress">The progress update to render.</param>
    public void Report(ScanProgress scanProgress)
    {
        lock (_gate)
        {
            switch (scanProgress.Phase)
            {
                case ScanPhase.Filtering:
                    FinishDynamicLineCore();
                    _writer.WriteLine($"  Se compararán {scanProgress.Completed} archivos de {scanProgress.Total} " +
                                      $"({scanProgress.Ignored} archivo(s) ignorados en {scanProgress.Elapsed.TotalSeconds:F1}s)");
                    break;

                case ScanPhase.Hashing:
                    RenderHashing(scanProgress);
                    break;

                case ScanPhase.Completed:
                    RenderCompleted(scanProgress);
                    break;
            }
        }

        //
        // Renders the hashing progress in-place if interactive, or as a new line otherwise.
        //
        void RenderHashing(ScanProgress scanProgress)
        {
            var candidates = scanProgress.Total - scanProgress.Ignored;
            var message = $"  Comparando {scanProgress.Completed} archivos de {candidates} " +
                          $"({scanProgress.Elapsed.TotalSeconds:F1}s)";

            // If not interactive, write the message as a new line and return
            if (!_interactive)
            {
                _writer.WriteLine(message);
                return;
            }

            // Else, move the cursor to the beginning of the line and overwrite the previous message
            _writer.Write('\r');
            _writer.Write(message);

            var padding = _lastRenderedLength - message.Length;
            if (padding > 0)
            {
                // Clear the remaining characters from the previous message
                for (int i = 0; i < padding; i++)
                    _writer.Write(' ');
            }

            // Update the last rendered length and mark the dynamic line as active
            _lastRenderedLength = message.Length;
            _dynamicLineActive = true;
        }

        //
        // Renders the completed progress in-place if interactive, or as a new line otherwise.
        //
        void RenderCompleted(ScanProgress scanProgress)
        {
            var message = $"  Completado: Se han comparado {scanProgress.Completed} archivos en {scanProgress.Elapsed.TotalSeconds:F1}s.";

            // If not interactive or no dynamic line is active, write the message as a new line and return
            if (!_interactive || !_dynamicLineActive)
            {
                FinishDynamicLineCore();
                _writer.WriteLine(message);
                return;
            }

            // Else, move the cursor to the beginning of the line and overwrite the previous message
            _writer.Write('\r');
            _writer.Write(message);

            var padding = _lastRenderedLength - message.Length;
            if (padding > 0)
            {
                // Clear the remaining characters from the previous message
                for (int i = 0; i < padding; i++)
                    _writer.Write(' ');
            }

            // Move to the next line after completing the message
            _writer.WriteLine();

            // Mark the dynamic line as inactive and reset the last rendered length
            _dynamicLineActive = false;
            _lastRenderedLength = 0;
        }
    }

    /// <summary>
    ///   Finishes an in-place progress line, if one is currently active.
    /// </summary>
    public void FinishDynamicLine()
    {
        lock (_gate)
        {
            FinishDynamicLineCore();
        }
    }


    /// <summary>
    ///   Finishes an in-place progress line, if one is currently active.
    /// </summary>
    private void FinishDynamicLineCore()
    {
        if (!_dynamicLineActive)
            return;

        _writer.WriteLine();
        _dynamicLineActive = false;
        _lastRenderedLength = 0;
    }
}
