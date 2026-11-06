using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Persistence;
using Xunit;

namespace Makredo.Core.Tests.Hooks;

public sealed class InjectedInputTests
{
    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, true)]
    public async Task KeyboardAndMouseOriginsAreFilteredBeforeRecording(bool injected, bool marked, bool enabled, bool expected)
    {
        var keyboard = new TestKeyboardHookSource();
        var mouse = new TimestampedMouse();
        var recorder = new MacroRecorderService(keyboard, mouse, new ApplicationStateService(), new MutableHotkeyConfiguration()) { RecordInjectedInput = enabled };
        await recorder.StartAsync("Origins");
        ulong extra = marked ? InputOriginPolicy.PlaybackSignature : 123;
        keyboard.Emit(new KeyboardActivityInfo(0x87, 0x76, true, false, HotkeyModifiers.None, false, HotkeyModifiers.None, null, injected, extra));
        mouse.Emit(new MouseActivityInfo(MouseActionType.MiddleDown, 1, 2, null, new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc), injected, extra));
        await recorder.StopAsync();
        Assert.Equal(expected ? 2 : 0, recorder.CurrentSession!.Events.Count);
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData("{\"RecordInjectedInput\":false}", false)]
    [InlineData("{\"RecordInjectedInput\":true}", true)]
    public async Task SettingsRemainCompatibleAndRoundTrip(string json, bool expected)
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(path, json);
            var store = new JsonPlaybackSettingsStore(path);
            PlaybackSettings settings = await store.LoadAsync();
            Assert.Equal(expected, settings.RecordInjectedInput);
            await store.SaveAsync(settings);
            Assert.Equal(expected, (await store.LoadAsync()).RecordInjectedInput);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class TimestampedMouse : ITimestampedMouseHookSource
    {
        public bool IsRunning { get; private set; }
        public event Action<MouseActivityInfo>? TimestampedMouseActivityReceived;
        public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived { add { } remove { } }
        public Task StartAsync(CancellationToken cancellationToken = default) { IsRunning = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken = default) { IsRunning = false; return Task.CompletedTask; }
        internal void Emit(MouseActivityInfo item) => TimestampedMouseActivityReceived?.Invoke(item);
    }
}
