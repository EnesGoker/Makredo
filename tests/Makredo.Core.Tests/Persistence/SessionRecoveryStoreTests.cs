using Makredo.Application.Abstractions;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Persistence;
using Xunit;
using static Makredo.Core.Tests.Support.TestData;

namespace Makredo.Core.Tests.Persistence;

public sealed class SessionRecoveryStoreTests
{
    [Fact]
    public async Task RecoveryRoundTripsAndReplacesTheCommittedFile()
    {
        string directory = CreateTempDirectory();
        try
        {
            AppStoragePaths paths = AppStoragePaths.FromRootDirectory(directory);
            Assert.Equal(Path.Combine(directory, "session-recovery.json"), paths.SessionRecoveryFilePath);
            var writer = new SessionRecoveryStore(paths.SessionRecoveryFilePath, new JsonMacroStorageService());
            MacroSession original = CreatePlaybackSession();

            await writer.SaveAsync(original);
            Assert.True(File.Exists(paths.SessionRecoveryFilePath));

            var reader = new SessionRecoveryStore(paths.SessionRecoveryFilePath, new JsonMacroStorageService());
            MacroSession restored = Assert.IsType<MacroSession>(await reader.TryLoadAsync());
            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.Events.Select(item => item.Id), restored.Events.Select(item => item.Id));
            Assert.Equal(original.Events.Select(item => item.ScanCode), restored.Events.Select(item => item.ScanCode));

            MacroSession replacement = CreatePlaybackSession();
            replacement.Name = "Replacement";
            await writer.SaveAsync(replacement);

            restored = Assert.IsType<MacroSession>(await reader.TryLoadAsync());
            Assert.Equal(replacement.Id, restored.Id);
            Assert.Equal(replacement.Name, restored.Name);
            Assert.Equal(paths.SessionRecoveryFilePath, Assert.Single(Directory.GetFiles(directory)));
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Fact]
    public async Task DeletingRecoveryRemovesItAndIsSafeToRepeat()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = AppStoragePaths.FromRootDirectory(directory).SessionRecoveryFilePath;
            var store = new SessionRecoveryStore(path, new JsonMacroStorageService());
            await store.SaveAsync(CreatePlaybackSession());

            await store.DeleteAsync();
            await store.DeleteAsync();

            Assert.False(File.Exists(path));
            Assert.Null(await store.TryLoadAsync());

            string missingDirectoryPath = AppStoragePaths.FromRootDirectory(Path.Combine(directory, "absent")).SessionRecoveryFilePath;
            await new SessionRecoveryStore(missingDirectoryPath, new JsonMacroStorageService()).DeleteAsync();
            Assert.False(Directory.Exists(Path.GetDirectoryName(missingDirectoryPath)));
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Theory]
    [InlineData("{\"Name\":")]
    [InlineData("{\"FormatVersion\":\"99\",\"Events\":[]}")]
    [InlineData("{\"Events\":[{\"EventType\":0,\"KeyboardActionType\":1,\"KeyCode\":65,\"DelayMs\":-1}]}")]
    [InlineData("""
        {"Events":[
            {"EventType":0,"KeyboardActionType":1,"KeyCode":65,"DelayMs":2147483647},
            {"EventType":0,"KeyboardActionType":2,"KeyCode":65,"DelayMs":1}
        ]}
        """)]
    public async Task InvalidRecoveryReturnsNoSessionAndLogsAWarning(string contents)
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = AppStoragePaths.FromRootDirectory(directory).SessionRecoveryFilePath;
            await File.WriteAllTextAsync(path, contents);
            var logger = new RecordingTestLogger();
            var store = new SessionRecoveryStore(path, new JsonMacroStorageService(), logger);

            Assert.Null(await store.TryLoadAsync());
            RecordedLogEntry warning = Assert.Single(logger.Entries);
            Assert.Equal(AppLogLevel.Warning, warning.LogLevel);
            Assert.NotNull(warning.Exception);
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Fact]
    public async Task InterruptedTemporaryFileNeverBecomesRecovery()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = AppStoragePaths.FromRootDirectory(directory).SessionRecoveryFilePath;
            var store = new SessionRecoveryStore(path, new JsonMacroStorageService());
            string stagingPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(stagingPath, "{\"Name\":");

            Assert.Null(await store.TryLoadAsync());

            MacroSession committed = CreatePlaybackSession();
            await store.SaveAsync(committed);
            Assert.Equal(committed.Id, Assert.IsType<MacroSession>(await store.TryLoadAsync()).Id);
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StagedSaveCannotOverwriteShutdownCheckpointOrUndoDeletion(bool checkpoint)
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = AppStoragePaths.FromRootDirectory(directory).SessionRecoveryFilePath;
            var jsonStorage = new JsonMacroStorageService();
            await new SessionRecoveryStore(path, jsonStorage).SaveAsync(CreatePlaybackSession());

            var delayedStorage = new DelayedJsonStorage(jsonStorage);
            var store = new SessionRecoveryStore(path, delayedStorage);
            MacroSession latest = CreatePlaybackSession();
            Task pendingSave = store.SaveAsync(CreatePlaybackSession());

            try
            {
                await delayedStorage.Staged.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
                if (checkpoint)
                {
                    store.SaveForShutdown(latest);
                    Assert.Equal(latest.Id, Assert.IsType<MacroSession>(await store.TryLoadAsync()).Id);
                }
                else
                {
                    await store.DeleteAsync();
                    Assert.False(File.Exists(path));
                }
            }
            finally
            {
                delayedStorage.Complete.TrySetResult();
                await pendingSave.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            }

            if (checkpoint)
            {
                Assert.Equal(latest.Id, Assert.IsType<MacroSession>(await store.TryLoadAsync()).Id);
                Assert.Equal(path, Assert.Single(Directory.GetFiles(directory)));
            }
            else
            {
                Assert.Null(await store.TryLoadAsync());
                Assert.Empty(Directory.GetFiles(directory));
            }
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    [Fact]
    public async Task CanceledRecoveryOperationsLeaveTheCommittedFileInPlace()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = AppStoragePaths.FromRootDirectory(directory).SessionRecoveryFilePath;
            var store = new SessionRecoveryStore(path, new JsonMacroStorageService());
            MacroSession committed = CreatePlaybackSession();
            await store.SaveAsync(committed);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(CreatePlaybackSession(), cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.TryLoadAsync(cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DeleteAsync(cancellation.Token));

            Assert.Equal(committed.Id, Assert.IsType<MacroSession>(await store.TryLoadAsync()).Id);
            Assert.Equal(path, Assert.Single(Directory.GetFiles(directory)));
        }
        finally
        {
            DeleteDirectoryIfExists(directory);
        }
    }

    private sealed class DelayedJsonStorage(IJsonMacroStorageService inner) : IJsonMacroStorageService
    {
        internal TaskCompletionSource Staged { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task SaveAsync(MacroSession session, string filePath, CancellationToken cancellationToken = default)
        {
            await inner.SaveAsync(session, filePath, cancellationToken);
            Staged.TrySetResult();
            await Complete.Task.WaitAsync(cancellationToken);
        }

        public Task<MacroSession> LoadAsync(string filePath, CancellationToken cancellationToken = default) =>
            inner.LoadAsync(filePath, cancellationToken);
    }
}
