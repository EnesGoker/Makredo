namespace MacroMaster.Application.Abstractions;

public interface IKeyboardHookSource
{
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    event Action<KeyboardActivityInfo>? KeyActivityReceived;
}
