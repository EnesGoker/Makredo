namespace MacroMaster.Application.Abstractions;

public interface IHotkeyConfiguration
{
    HotkeyBinding RecordToggleHotkey { get; }

    HotkeyBinding PlaybackToggleHotkey { get; }

    HotkeyBinding StopHotkey { get; }

    HotkeyBinding HotkeySettingsHotkey { get; }
}
