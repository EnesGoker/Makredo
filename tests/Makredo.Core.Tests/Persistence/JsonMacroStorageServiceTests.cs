using System.Text.Json;
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
using static Makredo.Core.Tests.Support.TestData;

namespace Makredo.Core.Tests.Persistence;

public sealed class JsonMacroStorageServiceTests
{
    [Fact(DisplayName = "JsonMacroStorageService round-trips a valid session")]
    public async Task JsonMacroStorageService_RoundTripsSession()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "roundtrip.json");
            var storageService = new JsonMacroStorageService();
            var session = new MacroSession
            {
                Name = "RoundTrip",
                Events =
                {
                    new MacroEvent
                    {
                        EventType = MacroEventType.Keyboard,
                        KeyboardActionType = KeyboardActionType.KeyDown,
                        DelayMs = 15,
                        KeyCode = 0x41,
                        ScanCode = 0x1E,
                        IsExtendedKey = false,
                        KeyName = "A",
                        Description = "Keyboard A down"
                    },
                    new MacroEvent
                    {
                        EventType = MacroEventType.Mouse,
                        MouseActionType = MouseActionType.Move,
                        DelayMs = 20,
                        X = 320,
                        Y = 240,
                        Description = "Mouse move"
                    }
                }
            };

            await storageService.SaveAsync(session, filePath);
            MacroSession loadedSession = await storageService.LoadAsync(filePath);

            Assert.Equal(MacroSessionFormat.CurrentVersion, loadedSession.FormatVersion);
            Assert.Equal(session.Name, loadedSession.Name);
            Assert.Equal(2, loadedSession.Events.Count);
            Assert.Equal(0x1E, loadedSession.Events[0].ScanCode);
            Assert.Equal(320, loadedSession.Events[1].X);
            Assert.Equal(240, loadedSession.Events[1].Y);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonMacroStorageService rejects unsupported format versions")]
    public async Task JsonMacroStorageService_RejectsUnsupportedVersion()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "unsupported.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Unsupported",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = "9.9",
                    Events = Array.Empty<object>()
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonMacroStorageService rejects system events during save")]
    public async Task JsonMacroStorageService_RejectsSystemEventOnSave()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "system-event.json");
            var storageService = new JsonMacroStorageService();
            var session = new MacroSession
            {
                Name = "SystemEventSave",
                Events =
                {
                    new MacroEvent
                    {
                        EventType = MacroEventType.System,
                        DelayMs = 5,
                        Description = "System event"
                    }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.SaveAsync(session, filePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonMacroStorageService rejects system events during load")]
    public async Task JsonMacroStorageService_RejectsSystemEventOnLoad()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "system-event-load.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "SystemEventLoad",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.System,
                            DelayMs = 10,
                            Description = "Unsupported system event"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonMacroStorageService rejects mouse click events without coordinates")]
    public async Task JsonMacroStorageService_RejectsMouseClickWithoutCoordinates()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "missing-click-coordinates.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidMouseClick",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.Mouse,
                            MouseActionType = MouseActionType.LeftDown,
                            DelayMs = 10,
                            Description = "Missing coordinates"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonMacroStorageService rejects invalid event type values")]
    public async Task JsonMacroStorageService_RejectsInvalidEventType()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-event-type.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidEventType",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = 99,
                            DelayMs = 10,
                            Description = "Invalid event type"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonMacroStorageService rejects invalid keyboard action values")]
    public async Task JsonMacroStorageService_RejectsInvalidKeyboardAction()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-keyboard-action.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidKeyboardAction",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.Keyboard,
                            KeyboardActionType = 99,
                            DelayMs = 10,
                            KeyCode = 0x41,
                            Description = "Invalid keyboard action"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonMacroStorageService rejects invalid mouse action values")]
    public async Task JsonMacroStorageService_RejectsInvalidMouseAction()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-mouse-action.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidMouseAction",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.Mouse,
                            MouseActionType = 99,
                            DelayMs = 10,
                            X = 100,
                            Y = 200,
                            Description = "Invalid mouse action"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath));
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }
}
