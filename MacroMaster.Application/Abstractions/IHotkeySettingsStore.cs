namespace MacroMaster.Application.Abstractions;

public interface IHotkeySettingsStore
{
    Task<HotkeySettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        HotkeySettings settings,
        CancellationToken cancellationToken = default);
}
