using MacroMaster.Domain.Enums;

namespace MacroMaster.Application.Abstractions;

public interface IMouseHookSource
{
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived;
}