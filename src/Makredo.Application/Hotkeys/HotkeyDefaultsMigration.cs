using Makredo.Application.Abstractions;

namespace Makredo.Application.Hotkeys;

public static class HotkeyDefaultsMigration
{
    public static bool ShouldUpgrade(HotkeySettings settings, bool pending) => pending
        && settings.RecordToggleHotkey == HotkeyBinding.None(0x77)
        && settings.PlaybackToggleHotkey == HotkeyBinding.None(0x78)
        && settings.StopHotkey == HotkeyBinding.None(0x79)
        && settings.HotkeySettingsHotkey == HotkeyBinding.None(0x7B);

    public static HotkeySettings LegacyPreset() => new()
    {
        RecordToggleHotkey = HotkeyBinding.None(0x77),
        PlaybackToggleHotkey = HotkeyBinding.None(0x78),
        StopHotkey = HotkeyBinding.None(0x79),
        HotkeySettingsHotkey = HotkeyBinding.None(0x7B)
    };
}
