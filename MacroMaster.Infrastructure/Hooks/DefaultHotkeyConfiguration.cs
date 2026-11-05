using MacroMaster.Application.Abstractions;

namespace MacroMaster.Infrastructure.Hooks;

public sealed class DefaultHotkeyConfiguration : IHotkeyConfiguration
{
    public DefaultHotkeyConfiguration(
        HotkeyBinding? recordToggle = null,
        HotkeyBinding? playbackToggle = null,
        HotkeyBinding? stop = null,
        HotkeyBinding? hotkeySettings = null)
    {
        RecordToggleHotkey = recordToggle ?? HotkeySettings.DefaultRecordToggleHotkey;
        PlaybackToggleHotkey = playbackToggle ?? HotkeySettings.DefaultPlaybackToggleHotkey;
        StopHotkey = stop ?? HotkeySettings.DefaultStopHotkey;
        HotkeySettingsHotkey = hotkeySettings ?? HotkeySettings.DefaultHotkeySettingsHotkey;
    }

    public HotkeyBinding RecordToggleHotkey { get; }

    public HotkeyBinding PlaybackToggleHotkey { get; }

    public HotkeyBinding StopHotkey { get; }

    public HotkeyBinding HotkeySettingsHotkey { get; }
}
