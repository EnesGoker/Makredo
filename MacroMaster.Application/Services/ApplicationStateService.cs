using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Enums;

namespace MacroMaster.Application.Services;

public sealed class ApplicationStateService : IApplicationStateService
{
    private static readonly Dictionary<AppState, AppState[]> AllowedTransitions =
        new()
        {
            [AppState.Idle] = [AppState.Recording, AppState.Playing, AppState.Error],
            [AppState.Recording] = [AppState.Idle, AppState.Stopping, AppState.Error],
            [AppState.Playing] = [AppState.Idle, AppState.Paused, AppState.Stopping, AppState.Error],
            [AppState.Paused] = [AppState.Idle, AppState.Playing, AppState.Stopping, AppState.Error],
            [AppState.Stopping] = [AppState.Idle, AppState.Error],
            [AppState.Error] = [AppState.Idle]
        };

    private readonly object _syncRoot = new();
    private AppState _currentState = AppState.Idle;

    public AppState CurrentState
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentState;
            }
        }
    }

    public event Action<AppState>? StateChanged;

    public bool IsState(AppState state)
    {
        lock (_syncRoot)
        {
            return _currentState == state;
        }
    }

    public bool IsAny(params AppState[] states)
    {
        ArgumentNullException.ThrowIfNull(states);

        lock (_syncRoot)
        {
            return states.Contains(_currentState);
        }
    }

    public bool TryTransitionTo(AppState newState, params AppState[] allowedCurrentStates)
    {
        ArgumentNullException.ThrowIfNull(allowedCurrentStates);

        AppState? changedState = null;

        lock (_syncRoot)
        {
            if (_currentState == newState)
            {
                return true;
            }

            if (allowedCurrentStates.Length > 0 && !allowedCurrentStates.Contains(_currentState))
            {
                return false;
            }

            EnsureTransitionIsAllowed(_currentState, newState);
            _currentState = newState;
            changedState = newState;
        }

        StateChanged?.Invoke(changedState.Value);
        return true;
    }

    public void SetState(AppState state)
    {
        AppState? changedState = null;

        lock (_syncRoot)
        {
            if (_currentState == state)
            {
                return;
            }

            EnsureTransitionIsAllowed(_currentState, state);
            _currentState = state;
            changedState = state;
        }

        StateChanged?.Invoke(changedState.Value);
    }

    private static void EnsureTransitionIsAllowed(AppState currentState, AppState newState)
    {
        if (!AllowedTransitions.TryGetValue(currentState, out AppState[]? validTransitions)
            || !validTransitions.Contains(newState))
        {
            throw new InvalidOperationException(
                $"Gecersiz uygulama durumu gecisi: {currentState} -> {newState}");
        }
    }
}
