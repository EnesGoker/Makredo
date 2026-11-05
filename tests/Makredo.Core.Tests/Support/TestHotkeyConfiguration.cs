using Makredo.Application.Abstractions;
using Makredo.Application.Filters;
using Makredo.Application.Reporting;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Diagnostics;
using Makredo.Infrastructure.Persistence;
using Xunit;

namespace Makredo.Core.Tests.Support;

internal sealed class TestHotkeyConfiguration : IHotkeyConfiguration
{
    public TestHotkeyConfiguration(
        HotkeyBinding recordToggleHotkey,
        HotkeyBinding playbackToggleHotkey,
        HotkeyBinding stopHotkey,
        HotkeyBinding? hotkeySettingsHotkey = null)
    {
        RecordToggleHotkey = recordToggleHotkey;
        PlaybackToggleHotkey = playbackToggleHotkey;
        StopHotkey = stopHotkey;
        HotkeySettingsHotkey = hotkeySettingsHotkey ?? HotkeySettings.DefaultHotkeySettingsHotkey;
    }

    public HotkeyBinding RecordToggleHotkey { get; }

    public HotkeyBinding PlaybackToggleHotkey { get; }

    public HotkeyBinding StopHotkey { get; }

    public HotkeyBinding HotkeySettingsHotkey { get; }
}
