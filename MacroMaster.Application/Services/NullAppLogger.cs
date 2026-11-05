using MacroMaster.Application.Abstractions;

namespace MacroMaster.Application.Services;

public sealed class NullAppLogger : IAppLogger
{
    public static NullAppLogger Instance { get; } = new();

    private NullAppLogger()
    {
    }

    public void Log(
        AppLogLevel logLevel,
        string source,
        string message,
        Exception? exception = null)
    {
        _ = logLevel;
        _ = source;
        _ = message;
        _ = exception;
    }
}
