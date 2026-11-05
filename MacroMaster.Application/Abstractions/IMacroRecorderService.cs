using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public interface IMacroRecorderService
{
    bool IsRecording { get; }

    /// <summary>
    /// Returns the active recording session while recording is in progress.
    /// After <see cref="StopAsync"/> completes successfully, this property keeps returning
    /// the completed session until <see cref="Clear"/> is called.
    /// </summary>
    MacroSession? CurrentSession { get; }

    Task StartAsync(
        string? sessionName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes the active recording and preserves the completed session in
    /// <see cref="CurrentSession"/> for subsequent preview, playback, or save operations.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the retained session reference after recording has completed.
    /// This method is rejected while a recording is still active.
    /// </summary>
    void Clear();

    event Action? RecordingStarted;
    event Action<MacroEvent>? EventRecorded;
    event Action<MacroSession>? RecordingStopped;
}
