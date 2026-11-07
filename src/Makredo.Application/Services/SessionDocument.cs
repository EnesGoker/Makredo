using Makredo.Application.Abstractions;
using Makredo.Domain.Models;

namespace Makredo.Application.Services;

public enum SessionTransition
{
    Close,
    NewRecording,
    Load,
    Clear
}

public enum UnsavedSessionChoice
{
    Save,
    Discard,
    Cancel
}

public sealed class SessionDocument(ISessionRecoveryStore recovery)
{
    public MacroSession? Session { get; private set; }
    public string? FilePath { get; private set; }
    public bool IsDirty { get; private set; }

    public void StartRecording(MacroSession session) => SetChangedSession(session, filePath: null);

    public Task CompleteRecordingAsync(MacroSession session, CancellationToken cancellationToken = default)
    {
        SetChangedSession(session, filePath: null);
        return recovery.SaveAsync(session, cancellationToken);
    }

    public Task EditEventAsync(int eventIndex, int delayMs, int? x, int? y, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delayMs);
        MacroSession session = GetRequiredSession();
        MacroEvent item = session.Events[eventIndex];
        item.DelayMs = delayMs;
        if (x.HasValue && y.HasValue)
        {
            item.X = x;
            item.Y = y;
        }

        return CheckpointChangeAsync(session, FilePath, cancellationToken);
    }

    public Task OptimizeAsync(IEnumerable<MacroEvent> events, CancellationToken cancellationToken = default)
    {
        MacroSession session = GetRequiredSession();
        session.ReplaceEvents(events);
        return CheckpointChangeAsync(session, FilePath, cancellationToken);
    }

    public Task RenameAsync(string name, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        MacroSession session = GetRequiredSession();
        session.Name = name;
        return CheckpointChangeAsync(session, filePath, cancellationToken);
    }

    public void DetachFile() => FilePath = null;

    public async Task LoadAsync(MacroSession session, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        await recovery.DeleteAsync(cancellationToken);
        SetSession(session, filePath, isDirty: false);
    }

    public async Task MarkSavedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        MacroSession session = GetRequiredSession();
        await recovery.DeleteAsync(cancellationToken);
        SetSession(session, filePath, isDirty: false);
    }

    public void Clear() => SetSession(null, null, isDirty: false);

    public async Task<bool> CanLeaveAsync(
        SessionTransition transition,
        Func<SessionTransition, UnsavedSessionChoice> decide,
        Func<MacroSession, CancellationToken, Task<string?>> save,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decide);
        ArgumentNullException.ThrowIfNull(save);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsDirty)
        {
            return true;
        }

        switch (decide(transition))
        {
            case UnsavedSessionChoice.Save:
                string? savedPath = await save(GetRequiredSession(), cancellationToken);
                if (savedPath is null)
                {
                    return false;
                }

                await MarkSavedAsync(savedPath, cancellationToken);
                return true;

            case UnsavedSessionChoice.Discard:
                // Keep the document until the caller actually replaces or closes it.
                await recovery.DeleteAsync(cancellationToken);
                return true;

            default:
                return false;
        }
    }

    public async Task<bool> OfferRecoveryAsync(Func<MacroSession, bool> restore, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(restore);
        MacroSession? session = await recovery.TryLoadAsync(cancellationToken);
        if (session is null)
        {
            return false;
        }

        if (!restore(session))
        {
            await recovery.DeleteAsync(cancellationToken);
            return false;
        }

        SetChangedSession(session, filePath: null);
        return true;
    }

    public void SaveForShutdown(MacroSession? recordingSnapshot = null)
    {
        MacroSession? checkpoint = recordingSnapshot ?? (IsDirty ? Session : null);
        if (checkpoint is not null)
        {
            recovery.SaveForShutdown(checkpoint);
        }
    }

    private Task CheckpointChangeAsync(MacroSession session, string? filePath, CancellationToken cancellationToken)
    {
        SetChangedSession(session, filePath);
        return recovery.SaveAsync(session, cancellationToken);
    }

    // New recordings, edits, optimizations, renames and restored sessions enter here.
    private void SetChangedSession(MacroSession session, string? filePath)
    {
        ArgumentNullException.ThrowIfNull(session);
        SetSession(session, filePath, isDirty: true);
    }

    private void SetSession(MacroSession? session, string? filePath, bool isDirty)
    {
        Session = session;
        FilePath = filePath;
        IsDirty = isDirty;
    }

    private MacroSession GetRequiredSession() => Session ?? throw new InvalidOperationException("Etkin bir makro oturumu yok.");
}
