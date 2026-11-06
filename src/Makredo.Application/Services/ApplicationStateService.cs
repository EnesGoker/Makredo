using System.Runtime.ExceptionServices;
using Makredo.Application.Abstractions;
using Makredo.Domain.Enums;

namespace Makredo.Application.Services;

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
    private readonly Queue<StateNotification> _pendingNotifications = new();
    private AppState _currentState = AppState.Idle;
    private bool _publishingNotifications;

    /// <summary>Gets the latest committed state, which can be newer than a queued notification.</summary>
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

    /// <summary>Publishes committed transitions in order, outside the state lock.</summary>
    /// <remarks>
    /// Subscribers are captured when the transition commits. Reentrant or concurrent transitions
    /// can return before their notifications are delivered. A subscriber exception stops the rest
    /// of that notification; queued notifications drain before the publisher rethrows the first exception.
    /// </remarks>
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

        bool publish;

        lock (_syncRoot)
        {
            if (_currentState == newState)
            {
                return allowedCurrentStates.Contains(_currentState);
            }

            if (allowedCurrentStates.Length > 0 && !allowedCurrentStates.Contains(_currentState))
            {
                return false;
            }

            EnsureTransitionIsAllowed(_currentState, newState);
            _currentState = newState;
            publish = EnqueueNotificationUnsafe(newState);
        }

        if (publish)
        {
            PublishNotifications();
        }

        return true;
    }

    public void SetState(AppState state)
    {
        bool publish;

        lock (_syncRoot)
        {
            if (_currentState == state)
            {
                return;
            }

            EnsureTransitionIsAllowed(_currentState, state);
            _currentState = state;
            publish = EnqueueNotificationUnsafe(state);
        }

        if (publish)
        {
            PublishNotifications();
        }
    }

    private bool EnqueueNotificationUnsafe(AppState state)
    {
        _pendingNotifications.Enqueue(new StateNotification(state, StateChanged));
        if (_publishingNotifications)
        {
            return false;
        }

        _publishingNotifications = true;
        return true;
    }

    private void PublishNotifications()
    {
        ExceptionDispatchInfo? firstFailure = null;
        while (true)
        {
            StateNotification notification;
            lock (_syncRoot)
            {
                if (!_pendingNotifications.TryDequeue(out notification))
                {
                    _publishingNotifications = false;
                    break;
                }
            }

            try
            {
                notification.Subscribers?.Invoke(notification.State);
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
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

    private readonly record struct StateNotification(AppState State, Action<AppState>? Subscribers);
}
