namespace MacroMaster.Application.Abstractions;

public interface IMacroLibraryUserStateStore
{
    Task<MacroLibraryUserState> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        MacroLibraryUserState state,
        CancellationToken cancellationToken = default);
}
