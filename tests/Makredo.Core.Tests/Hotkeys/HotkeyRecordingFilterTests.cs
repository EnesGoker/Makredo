using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Xunit;

namespace Makredo.Core.Tests.Hotkeys;

public sealed class HotkeyRecordingFilterTests
{
    private const HotkeyModifiers Chord = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt;
    private static HotkeyRecordingFilter Create(params int[] held) => new([new(0x52, Chord), new(0x50, Chord)], held);
    internal static KeyboardActivityInfo Key(int key, bool down = true, HotkeyModifiers modifiers = HotkeyModifiers.None) =>
        new(key, 0, down, key == 0xA3, modifiers, HotkeyRecordingFilter.ModifierForKey(key) != HotkeyModifiers.None, HotkeyRecordingFilter.ModifierForKey(key));

    [Fact]
    public void StartingGestureReleasesAreIgnoredButSubsequentTypingIsPreserved()
    {
        var filter = Create(0xA2, 0xA0, 0xA4, 0x52);
        foreach (int key in new[] { 0x52, 0xA4, 0xA0, 0xA2 }) { Assert.Empty(filter.Process(Key(key, false))); }
        Assert.Empty(filter.Process(Key(0xA2, true, HotkeyModifiers.Control)));
        Assert.Equal<int>([0xA2, 0x41], filter.Process(Key(0x41, true, HotkeyModifiers.Control)).Select(input => input.VirtualKeyCode));
    }

    [Fact]
    public void MissedStartupReleaseDoesNotSuppressFreshUserInput()
    {
        var filter = Create(0xA2, 0x52);
        Assert.Empty(filter.Process(Key(0xA2, true, HotkeyModifiers.Control)));
        Assert.Equal<int>([0xA2, 0x52], filter.Process(Key(0x52, true, HotkeyModifiers.Control)).Select(input => input.VirtualKeyCode));
    }

    [Theory]
    [InlineData(0x52)]
    [InlineData(0x50)]
    public void CompleteCommandAndRepeatedPrimaryAreExcluded(int primary)
    {
        var filter = Create();
        Assert.Empty(filter.Process(Key(0xA2, true, HotkeyModifiers.Control)));
        Assert.Empty(filter.Process(Key(0xA0, true, HotkeyModifiers.Control | HotkeyModifiers.Shift)));
        Assert.Empty(filter.Process(Key(0xA4, true, Chord)));
        Assert.Empty(filter.Process(Key(primary, true, Chord)));
        Assert.Empty(filter.Process(Key(primary, true, Chord)));
        Assert.Empty(filter.Process(Key(primary, false, Chord)));
        Assert.Empty(filter.Process(Key(0xA4, false, HotkeyModifiers.Control | HotkeyModifiers.Shift)));
        Assert.Empty(filter.Process(Key(0xA0, false, HotkeyModifiers.Control)));
        Assert.Empty(filter.Process(Key(0xA2, false)));
        Assert.Empty(filter.Flush());
    }

    [Fact]
    public void UserControlBeforeInterveningKeyIsNotRemovedByStopGesture()
    {
        var filter = Create();
        Assert.Empty(filter.Process(Key(0xA3, true, HotkeyModifiers.Control)));
        var typed = filter.Process(Key(0x41, true, HotkeyModifiers.Control));
        Assert.Equal<int>([0xA3, 0x41], typed.Select(input => input.VirtualKeyCode));
        Assert.True(typed[0].IsExtendedKey);
        Assert.Single(filter.Process(Key(0x41, false, HotkeyModifiers.Control)));
        Assert.Empty(filter.Process(Key(0xA0, true, HotkeyModifiers.Control | HotkeyModifiers.Shift)));
        Assert.Empty(filter.Process(Key(0xA4, true, Chord)));
        Assert.Empty(filter.Process(Key(0x50, true, Chord)));
        Assert.Single(filter.Process(Key(0xA3, false, HotkeyModifiers.Shift | HotkeyModifiers.Alt)));
    }

    [Fact]
    public void MouseBoundaryAndClosedModifierTapPreserveUserModifiers()
    {
        var filter = Create();
        Assert.Empty(filter.Process(Key(0xA2, true, HotkeyModifiers.Control)));
        Assert.Single(filter.Flush());
        Assert.Single(filter.Process(Key(0xA2, false)));
        Assert.Empty(filter.Process(Key(0xA0, true, HotkeyModifiers.Shift)));
        Assert.Equal(2, filter.Process(Key(0xA0, false)).Count);
    }

    [Fact]
    public void SettingsKeyIsOrdinaryRecordedInput()
    {
        var filter = Create();
        Assert.Single(filter.Process(Key(0x7B)));
        Assert.Single(filter.Process(Key(0x7B, false)));
    }
}
