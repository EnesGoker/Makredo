using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Session;

public sealed class SessionDocumentTests
{
    private static readonly string LoadedPath = Path.Combine(Path.GetTempPath(), "loaded-makredo.json");
    private static readonly string SavedPath = Path.Combine(Path.GetTempPath(), "saved-makredo.json");

    [Theory]
    [InlineData(SessionTransition.Close, UnsavedSessionChoice.Save)]
    [InlineData(SessionTransition.Close, UnsavedSessionChoice.Discard)]
    [InlineData(SessionTransition.Close, UnsavedSessionChoice.Cancel)]
    [InlineData(SessionTransition.NewRecording, UnsavedSessionChoice.Save)]
    [InlineData(SessionTransition.NewRecording, UnsavedSessionChoice.Discard)]
    [InlineData(SessionTransition.NewRecording, UnsavedSessionChoice.Cancel)]
    [InlineData(SessionTransition.Load, UnsavedSessionChoice.Save)]
    [InlineData(SessionTransition.Load, UnsavedSessionChoice.Discard)]
    [InlineData(SessionTransition.Load, UnsavedSessionChoice.Cancel)]
    [InlineData(SessionTransition.Clear, UnsavedSessionChoice.Save)]
    [InlineData(SessionTransition.Clear, UnsavedSessionChoice.Discard)]
    [InlineData(SessionTransition.Clear, UnsavedSessionChoice.Cancel)]
    public async Task DirtySessionResolvesEveryLeaveChoice(SessionTransition transition, UnsavedSessionChoice choice)
    {
        var recovery = new MemoryRecoveryStore();
        var document = new SessionDocument(recovery);
        await RecordAsync(document);
        MacroSession original = Assert.IsType<MacroSession>(document.Session);
        var decisions = new List<SessionTransition>();
        var saved = new List<MacroSession>();

        bool mayLeave = await document.CanLeaveAsync(transition,
            requested =>
            {
                decisions.Add(requested);
                return choice;
            },
            (session, _) =>
            {
                saved.Add(session);
                return Task.FromResult<string?>(SavedPath);
            });

        Assert.Equal(transition, Assert.Single(decisions));
        Assert.Same(original, document.Session);
        Assert.Equal(choice != UnsavedSessionChoice.Cancel, mayLeave);
        Assert.Equal(choice != UnsavedSessionChoice.Save, document.IsDirty);

        if (choice == UnsavedSessionChoice.Save)
        {
            Assert.Same(original, Assert.Single(saved));
            Assert.Equal(SavedPath, document.FilePath);
        }
        else
        {
            Assert.Empty(saved);
            Assert.Null(document.FilePath);
        }

        if (choice == UnsavedSessionChoice.Cancel)
        {
            Assert.Equal(original.Id, recovery.Snapshot?.Id);
        }
        else
        {
            Assert.Null(recovery.Snapshot);
        }
    }

    [Fact]
    public async Task CancelingTheSaveDialogAlsoCancelsTheRequestedTransition()
    {
        var recovery = new MemoryRecoveryStore();
        var document = new SessionDocument(recovery);
        await RecordAsync(document);
        MacroSession original = Assert.IsType<MacroSession>(document.Session);

        bool mayLeave = await document.CanLeaveAsync(SessionTransition.Load,
            _ => UnsavedSessionChoice.Save,
            (_, _) => Task.FromResult<string?>(null));

        Assert.False(mayLeave);
        Assert.True(document.IsDirty);
        Assert.Same(original, document.Session);
        Assert.Equal(original.Id, recovery.Snapshot?.Id);
    }

    [Fact]
    public async Task ALoadedSessionRemainsCleanAfterPlaybackAndNeedsNoPrompt()
    {
        var recovery = new MemoryRecoveryStore();
        var document = new SessionDocument(recovery);
        MacroSession loaded = CreateSession();
        await document.LoadAsync(loaded, LoadedPath);
        var adapter = new TestInputPlaybackAdapter();
        await using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());
        await playback.PlayAsync(Assert.IsType<MacroSession>(document.Session),
            new PlaybackSettings { PreserveOriginalTiming = false });

        Assert.Single(adapter.PlayedEvents);
        Assert.False(document.IsDirty);
        Assert.Equal(LoadedPath, document.FilePath);
        Assert.True(await document.CanLeaveAsync(SessionTransition.Close,
            _ => throw new InvalidOperationException("A clean document must not prompt."),
            (_, _) => throw new InvalidOperationException("A clean document must not save.")));
        Assert.Equal(0, recovery.SaveCount);
    }

    [Fact]
    public async Task RecordingMarksDirtyAndCompletionWritesTheRecoverySnapshot()
    {
        var recovery = new MemoryRecoveryStore();
        var document = new SessionDocument(recovery);
        MacroSession recording = CreateSession();

        document.StartRecording(recording);

        Assert.True(document.IsDirty);
        Assert.Null(document.FilePath);

        await document.CompleteRecordingAsync(recording);

        Assert.True(document.IsDirty);
        Assert.Equal(recording.Id, recovery.Snapshot?.Id);
        Assert.Single(Assert.IsType<MacroSession>(recovery.Snapshot).Events);
        Assert.Equal(1, recovery.SaveCount);
    }

    [Fact]
    public async Task EditingAnEventMarksALoadedSessionDirty()
    {
        SessionDocument document = await LoadDocumentAsync();

        await document.EditEventAsync(0, 15, 30, 40);

        Assert.True(document.IsDirty);
        MacroEvent edited = Assert.Single(Assert.IsType<MacroSession>(document.Session).Events);
        Assert.Equal(15, edited.DelayMs);
        Assert.Equal(30, edited.X);
        Assert.Equal(40, edited.Y);
    }

    [Fact]
    public async Task OptimizationMarksALoadedSessionDirty()
    {
        SessionDocument document = await LoadDocumentAsync();
        MacroEvent optimized = CreateMove(delayMs: 7);

        await document.OptimizeAsync([optimized]);

        Assert.True(document.IsDirty);
        Assert.Equal(optimized.Id, Assert.Single(Assert.IsType<MacroSession>(document.Session).Events).Id);
    }

    [Fact]
    public async Task RenameMarksALoadedSessionDirtyAndUpdatesItsPath()
    {
        SessionDocument document = await LoadDocumentAsync();

        await document.RenameAsync("Renamed session", SavedPath);

        Assert.True(document.IsDirty);
        Assert.Equal("Renamed session", document.Session?.Name);
        Assert.Equal(SavedPath, document.FilePath);
    }

    [Fact]
    public async Task SavingClosesTheDirtyFlagAndDeletesRecovery()
    {
        var recovery = new MemoryRecoveryStore();
        var document = new SessionDocument(recovery);
        await RecordAsync(document);

        await document.MarkSavedAsync(SavedPath);

        Assert.False(document.IsDirty);
        Assert.Equal(SavedPath, document.FilePath);
        Assert.Null(recovery.Snapshot);
    }

    [Fact]
    public async Task LoadingReplacesTheDirtySessionAndDeletesRecovery()
    {
        var recovery = new MemoryRecoveryStore();
        var document = new SessionDocument(recovery);
        await RecordAsync(document);
        MacroSession replacement = CreateSession();

        await document.LoadAsync(replacement, LoadedPath);

        Assert.Equal(replacement.Id, document.Session?.Id);
        Assert.Equal(LoadedPath, document.FilePath);
        Assert.False(document.IsDirty);
        Assert.Null(recovery.Snapshot);
    }

    [Fact]
    public async Task ClearingAnAllowedDocumentResetsItsSessionAndPath()
    {
        SessionDocument document = await LoadDocumentAsync();

        document.Clear();

        Assert.Null(document.Session);
        Assert.Null(document.FilePath);
        Assert.False(document.IsDirty);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RecoveryOfferRestoresADirtyDocumentOrDeletesADeclinedSnapshot(bool accept)
    {
        MacroSession snapshot = CreateSession();
        var recovery = new MemoryRecoveryStore { Snapshot = snapshot };
        var document = new SessionDocument(recovery);
        var offered = new List<MacroSession>();

        bool restored = await document.OfferRecoveryAsync(session =>
        {
            offered.Add(session);
            return accept;
        });

        Assert.Equal(snapshot.Id, Assert.Single(offered).Id);
        Assert.Equal(accept, restored);
        Assert.Equal(accept, document.IsDirty);
        Assert.Null(document.FilePath);

        if (accept)
        {
            Assert.Equal(snapshot.Id, document.Session?.Id);
            Assert.Equal(snapshot.Id, recovery.Snapshot?.Id);
        }
        else
        {
            Assert.Null(document.Session);
            Assert.Null(recovery.Snapshot);
            Assert.False(await new SessionDocument(recovery).OfferRecoveryAsync(
                _ => throw new InvalidOperationException("Declined recovery must not be offered again.")));
        }
    }

    [Fact]
    public async Task ShutdownWritesDirtyDataOrTheCurrentRecordingSnapshotWithoutPrompting()
    {
        var recovery = new MemoryRecoveryStore();
        var document = new SessionDocument(recovery);
        await document.LoadAsync(CreateSession(), LoadedPath);

        document.SaveForShutdown();

        Assert.Null(recovery.Snapshot);
        Assert.Equal(0, recovery.ShutdownSaveCount);

        await document.EditEventAsync(0, 11, 30, 40);
        document.SaveForShutdown();

        Assert.Equal(document.Session?.Id, recovery.Snapshot?.Id);
        Assert.Equal(1, recovery.ShutdownSaveCount);
        MacroSession liveRecording = CreateSession();

        document.SaveForShutdown(liveRecording);

        Assert.Equal(liveRecording.Id, recovery.Snapshot?.Id);
        Assert.Equal(2, recovery.ShutdownSaveCount);
    }

    private static async Task<SessionDocument> LoadDocumentAsync()
    {
        var document = new SessionDocument(new MemoryRecoveryStore());
        await document.LoadAsync(CreateSession(), LoadedPath);
        return document;
    }

    private static async Task RecordAsync(SessionDocument document)
    {
        MacroSession recording = CreateSession();
        document.StartRecording(recording);
        await document.CompleteRecordingAsync(recording);
    }

    private static MacroSession CreateSession() =>
        new() { Name = "Session document test", Events = { CreateMove(50) } };

    private static MacroEvent CreateMove(int delayMs) =>
        new()
        {
            EventType = MacroEventType.Mouse,
            MouseActionType = MouseActionType.Move,
            DelayMs = delayMs,
            X = 10,
            Y = 20
        };

    private sealed class MemoryRecoveryStore : ISessionRecoveryStore
    {
        public MacroSession? Snapshot { get; set; }

        public int SaveCount { get; private set; }

        public int ShutdownSaveCount { get; private set; }

        public Task SaveAsync(MacroSession session, CancellationToken cancellationToken = default)
        {
            Snapshot = session;
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task<MacroSession?> TryLoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public void SaveForShutdown(MacroSession session)
        {
            Snapshot = session;
            ShutdownSaveCount++;
        }
    }
}
