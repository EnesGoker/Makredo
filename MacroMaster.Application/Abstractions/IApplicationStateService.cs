using MacroMaster.Domain.Enums;

namespace MacroMaster.Application.Abstractions;

public interface IApplicationStateService
{
    AppState CurrentState { get; }

    bool IsState(AppState state);

    bool IsAny(params AppState[] states);

    bool TryTransitionTo(AppState newState, params AppState[] allowedCurrentStates);

    void SetState(AppState state);

    event Action<AppState>? StateChanged;
}
