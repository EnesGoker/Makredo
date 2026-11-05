namespace MacroMaster.Application.Abstractions;

public interface IAppLogger
{
    void Log(
        AppLogLevel logLevel,
        string source,
        string message,
        Exception? exception = null);
}
