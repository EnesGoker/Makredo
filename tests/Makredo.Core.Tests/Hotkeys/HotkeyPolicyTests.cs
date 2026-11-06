using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Xunit;

namespace Makredo.Core.Tests.Hotkeys;

public sealed class HotkeyPolicyTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void MigrationRequiresPendingFlagAndExactLegacyDefaults(bool pending, bool custom, bool expected)
    {
        var settings = HotkeyDefaultsMigration.LegacyPreset();
        if (custom) { settings.RecordToggleHotkey = HotkeyBinding.None(0x70); }
        Assert.Equal(expected, HotkeyDefaultsMigration.ShouldUpgrade(settings, pending));
    }

    [Fact]
    public void AllLegacyFieldsMustMatchIncludingInactiveSettingsBinding()
    {
        var settings = HotkeyDefaultsMigration.LegacyPreset();
        settings.HotkeySettingsHotkey = HotkeyBinding.None(0x70);
        Assert.False(HotkeyDefaultsMigration.ShouldUpgrade(settings, true));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EachRegistrationSurvivesFailureOfAnother(int failedId)
    {
        var requests = Enumerable.Range(1, 3).Select(id => new HotkeyRegistrationRequest(id, HotkeyBinding.None(0x70 + id))).ToArray();
        List<int> attempted = [], registered = [];
        var failures = HotkeyRegistrationBatch.Register(requests, request =>
        {
            attempted.Add(request.Id);
            if (request.Id == failedId) { throw new InvalidOperationException("Çakışma"); }
            registered.Add(request.Id);
        });
        Assert.Equal<int>([1, 2, 3], attempted);
        Assert.Equal(attempted.Where(id => id != failedId), registered);
        Assert.Equal(requests[failedId - 1].Binding, Assert.Single(failures).Binding);
        Assert.Equal("Çakışma", failures[0].Reason);
    }

    [Fact]
    public void DefaultsHaveTwoChordsAndPermitOnlyPlaybackStopAlias()
    {
        var settings = HotkeySettings.CreateDefault();
        const HotkeyModifiers modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt;
        Assert.Equal(new(0x52, modifiers), settings.RecordToggleHotkey);
        Assert.Equal(new(0x50, modifiers), settings.PlaybackToggleHotkey);
        Assert.Equal(settings.PlaybackToggleHotkey, settings.StopHotkey);
        HotkeySettingsValidator.Validate(settings, "Defaults");
        settings.RecordToggleHotkey = settings.PlaybackToggleHotkey;
        Assert.Throws<InvalidOperationException>(() => HotkeySettingsValidator.Validate(settings, "Duplicate"));
    }
}
