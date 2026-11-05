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

namespace Makredo.Core.Tests.Library;

public sealed class MacroLibraryServiceTests
{
    [Fact(DisplayName = "MacroLibraryService reads duration metadata")]
    public async Task MacroLibraryService_ReadsDurationMetadata()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            var storageService = new MacroStorageService(
                new JsonMacroStorageService(),
                new XmlMacroStorageService());
            var libraryService = new MacroLibraryService(
                storageService,
                new RecordingTestLogger(),
                directoryPath);

            await storageService.SaveAsJsonAsync(
                CreateSession("JsonMacro", 15, 20),
                Path.Combine(directoryPath, "json-macro.json"));
            await storageService.SaveAsXmlAsync(
                CreateSession("XmlMacro", 30, 45),
                Path.Combine(directoryPath, "xml-macro.xml"));

            IReadOnlyList<MacroLibraryEntry> entries = await libraryService.ListAsync();

            MacroLibraryEntry jsonEntry = entries.Single(entry => entry.Format == MacroLibraryFileFormat.Json);
            MacroLibraryEntry xmlEntry = entries.Single(entry => entry.Format == MacroLibraryFileFormat.Xml);

            Assert.Equal(2, jsonEntry.EventCount);
            Assert.Equal(35, jsonEntry.TotalDurationMs);
            Assert.Equal(2, xmlEntry.EventCount);
            Assert.Equal(75, xmlEntry.TotalDurationMs);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "MacroLibraryService imports XML files into the library")]
    public async Task MacroLibraryService_ImportsXmlFilesIntoLibrary()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string libraryDirectoryPath = Path.Combine(directoryPath, "library");
            string sourceDirectoryPath = Path.Combine(directoryPath, "source");
            Directory.CreateDirectory(sourceDirectoryPath);

            var storageService = new MacroStorageService(
                new JsonMacroStorageService(),
                new XmlMacroStorageService());
            var libraryService = new MacroLibraryService(
                storageService,
                new RecordingTestLogger(),
                libraryDirectoryPath);

            string sourceFilePath = Path.Combine(sourceDirectoryPath, "DesktopMacro.xml");
            await storageService.SaveAsXmlAsync(
                CreateSession("EmbeddedMacroName", 8, 13),
                sourceFilePath);

            string importedFilePath = await libraryService.ImportAsync(sourceFilePath);
            IReadOnlyList<MacroLibraryEntry> entries = await libraryService.ListAsync();
            MacroSession importedSession = await libraryService.LoadAsync(importedFilePath);

            Assert.True(
                importedFilePath.StartsWith(libraryDirectoryPath, StringComparison.OrdinalIgnoreCase),
                "Imported XML files should be copied into the managed library directory.");
            Assert.True(
                File.Exists(importedFilePath),
                "Imported XML file should exist in the managed library directory.");
            Assert.Equal(
                "DesktopMacro",
                entries.Single().Name);
            Assert.Equal(
                "DesktopMacro.xml",
                Path.GetFileName(importedFilePath));
            Assert.Equal(
                "DesktopMacro",
                importedSession.Name);
            Assert.Equal(
                MacroLibraryFileFormat.Xml,
                entries.Single().Format);
            Assert.Equal(
                21,
                entries.Single().TotalDurationMs);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }
}
