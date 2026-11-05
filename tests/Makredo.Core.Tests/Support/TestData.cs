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

internal static class TestData
{
    internal static MacroSession CreatePlaybackSession()
    {
        return new MacroSession
        {
            Name = "Playback",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A down"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyUp,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A up"
                }
            }
        };
    }

    internal static MacroSession CreateThreeEventPlaybackSession()
    {
        return new MacroSession
        {
            Name = "SteppablePlayback",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A down"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyUp,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A up"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x42,
                    ScanCode = 0x30,
                    Description = "B down"
                }
            }
        };
    }

    internal static MacroSession CreateSession(string name, params int[] delayValuesMs)
    {
        var session = new MacroSession
        {
            Name = name
        };

        for (int index = 0; index < delayValuesMs.Length; index++)
        {
            session.Events.Add(
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    DelayMs = delayValuesMs[index],
                    X = 100 + index,
                    Y = 200 + index,
                    Description = $"Mouse move {index}"
                });
        }

        return session;
    }

    internal static MacroEvent CreateMouseMoveEvent(int delayMs, int x, int y)
    {
        return new MacroEvent
        {
            EventType = MacroEventType.Mouse,
            MouseActionType = MouseActionType.Move,
            DelayMs = delayMs,
            X = x,
            Y = y,
            Description = "Fare hareketi"
        };
    }

    internal static MacroLibraryViewItem CreateLibraryViewItem(
        string name,
        string fileName,
        DateTime lastModifiedUtc,
        int eventCount,
        int totalDurationMs,
        MacroLibraryFileFormat format,
        bool isFavorite,
        DateTime? lastUsedUtc)
    {
        return new MacroLibraryViewItem(
            new MacroLibraryEntry(
                name,
                Path.Combine(Path.GetTempPath(), fileName),
                lastModifiedUtc,
                eventCount,
                totalDurationMs,
                format),
            isFavorite,
            lastUsedUtc);
    }

    internal static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "Makredo.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    internal static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
