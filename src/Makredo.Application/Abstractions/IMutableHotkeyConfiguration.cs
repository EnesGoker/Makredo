namespace Makredo.Application.Abstractions;

public interface IMutableHotkeyConfiguration : IHotkeyConfiguration
{
    void Apply(HotkeySettings settings);

    HotkeySettings Snapshot();
}
