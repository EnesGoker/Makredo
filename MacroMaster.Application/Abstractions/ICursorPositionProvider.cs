namespace MacroMaster.Application.Abstractions;

public interface ICursorPositionProvider
{
    Task<CursorPosition> GetCursorPositionAsync(
        CancellationToken cancellationToken = default);
}
