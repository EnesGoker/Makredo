using Makredo.Domain.Models;

namespace Makredo.Application.Abstractions;

public interface ISessionRecoveryStore
{
    Task SaveAsync(MacroSession session, CancellationToken cancellationToken = default);

    void SaveForShutdown(MacroSession session);

    Task<MacroSession?> TryLoadAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
