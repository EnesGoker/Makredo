using Makredo.Application.Abstractions;
using Makredo.Platform.Windows.Hooks;
using Xunit;

namespace Makredo.Windows.Tests.Hotkeys;

public sealed class HeldModifierTests
{
    [Theory]
    [InlineData(0xA2, 0xA3, HotkeyModifiers.Control)]
    [InlineData(0xA0, 0xA1, HotkeyModifiers.Shift)]
    [InlineData(0xA4, 0xA5, HotkeyModifiers.Alt)]
    public void ReleasingOneSidePreservesOtherHeldModifier(int left, int right, HotkeyModifiers expected)
    {
        using var source = new WindowsKeyboardHookSource();
        var received = new List<KeyboardActivityInfo>();
        source.KeyActivityReceived += received.Add;
        source.ProcessSample(Sample(left, true));
        source.ProcessSample(Sample(right, true));
        source.ProcessSample(Sample(left, false));
        Assert.Equal(expected, received[^1].ActiveModifiers);
        source.ProcessSample(Sample(right, false));
        Assert.Equal(HotkeyModifiers.None, received[^1].ActiveModifiers);
    }

    private static HookSample Sample(int key, bool down) => new(HookKind.Keyboard, down ? 0x0100 : 0x0101,
        (uint)key, 0, 0, 100, 0, 0, 0, new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc));
}
