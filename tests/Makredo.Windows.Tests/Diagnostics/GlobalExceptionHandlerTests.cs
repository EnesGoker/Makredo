using System;
using System.Linq;
using System.Threading.Tasks;
using Makredo.Windows.Tests.Support;
using Makredo.WinForms;
using Xunit;

namespace Makredo.Windows.Tests;

public sealed class GlobalExceptionHandlerTests
{
    [Fact(DisplayName = "GlobalExceptionHandlerRegistration logs UI thread exceptions")]
    public Task GlobalExceptionHandlerRegistration_LogsThreadExceptions()
    {
        var logger = new RecordingTestLogger();
        string? shownMessage = null;

        using var registration = new GlobalExceptionHandlerRegistration(
            logger,
            (message, _, _) => shownMessage = message,
            registerHandlers: false);

        registration.HandleThreadException(new InvalidOperationException("UI thread boom"));

        Assert.True(
            logger.Entries.Any(entry =>
                entry.Source == nameof(GlobalExceptionHandlerRegistration)
                && entry.Message.Contains("UI is parcaciginda", StringComparison.Ordinal)),
            "Global exception handler should log UI thread exceptions.");
        Assert.True(
            !string.IsNullOrWhiteSpace(shownMessage),
            "Global exception handler should surface a user-facing error message for UI thread exceptions.");
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "GlobalExceptionHandlerRegistration logs unobserved task exceptions")]
    public Task GlobalExceptionHandlerRegistration_LogsUnobservedTaskExceptions()
    {
        var logger = new RecordingTestLogger();

        using var registration = new GlobalExceptionHandlerRegistration(
            logger,
            registerHandlers: false);

        registration.HandleUnobservedTaskException(
            new AggregateException(new InvalidOperationException("Task boom")));

        Assert.True(
            logger.Entries.Any(entry =>
                entry.Source == nameof(GlobalExceptionHandlerRegistration)
                && entry.Message.Contains("Gozlemlenmemis gorev hatasi", StringComparison.Ordinal)),
            "Global exception handler should log unobserved task exceptions.");
        return Task.CompletedTask;
    }
}
