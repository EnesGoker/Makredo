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

public sealed class XmlMacroStorageServiceTests
{
    [Fact(DisplayName = "XmlMacroStorageService round-trips a valid session")]
    public async Task XmlMacroStorageService_RoundTripsSession()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "roundtrip.xml");
            var storageService = new XmlMacroStorageService();
            var session = new MacroSession
            {
                Name = "XmlRoundTrip",
                Events =
                {
                    new MacroEvent
                    {
                        EventType = MacroEventType.Keyboard,
                        KeyboardActionType = KeyboardActionType.KeyDown,
                        DelayMs = 10,
                        KeyCode = 0x41,
                        ScanCode = 0x1E,
                        Description = "Keyboard A down"
                    },
                    new MacroEvent
                    {
                        EventType = MacroEventType.Mouse,
                        MouseActionType = MouseActionType.Move,
                        DelayMs = 25,
                        X = 640,
                        Y = 360,
                        Description = "Mouse move"
                    }
                }
            };

            await storageService.SaveAsync(session, filePath);
            MacroSession loadedSession = await storageService.LoadAsync(filePath);

            Assert.Equal(session.Name, loadedSession.Name);
            Assert.Equal(2, loadedSession.Events.Count);
            Assert.Equal(0x41, loadedSession.Events[0].KeyCode);
            Assert.Equal(640, loadedSession.Events[1].X);
            Assert.Equal(360, loadedSession.Events[1].Y);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }
}
