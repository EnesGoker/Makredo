using System;
using System.Collections.Generic;
using Makredo.Application.Abstractions;

namespace Makredo.Core.Tests.Support;

internal sealed class RecordingTestLogger : IAppLogger
{
    public List<RecordedLogEntry> Entries { get; } = [];

    public void Log(
        AppLogLevel logLevel,
        string source,
        string message,
        Exception? exception = null)
    {
        Entries.Add(new RecordedLogEntry(logLevel, source, message, exception));
    }
}

internal readonly record struct RecordedLogEntry(
    AppLogLevel LogLevel,
    string Source,
    string Message,
    Exception? Exception);
