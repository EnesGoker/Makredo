using Makredo.Application.Abstractions;
using Makredo.Application.Filters;
using Makredo.Application.Reporting;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Diagnostics;
using Makredo.Infrastructure.Persistence;
using Xunit;

namespace Makredo.Core.Tests.Support;

internal sealed class TestMouseHookSource : IMouseHookSource
{
    public bool IsRunning { get; private set; }

    public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = false;
        return Task.CompletedTask;
    }

    public void Emit(MouseActionType mouseActionType, int? x, int? y, int? wheelDelta)
    {
        MouseActivityReceived?.Invoke(mouseActionType, x, y, wheelDelta);
    }
}
