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

namespace Makredo.Core.Tests.Filtering;

public sealed class MacroLibraryFilterTests
{
    [Fact(DisplayName = "MacroLibraryFilterEngine applies search and library filters")]
    public Task MacroLibraryFilterEngine_AppliesSearchAndFilters()
    {
        DateTime now = new(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);
        MacroLibraryViewItem[] items =
        [
            CreateLibraryViewItem("FavoriteJson", "favorite.json", now, 50, 5_000, MacroLibraryFileFormat.Json, isFavorite: true, lastUsedUtc: null),
            CreateLibraryViewItem("RecentXml", "recent.xml", now.AddMinutes(-1), 150, 12_000, MacroLibraryFileFormat.Xml, isFavorite: false, lastUsedUtc: now),
            CreateLibraryViewItem("LongJson", "long.json", now.AddMinutes(-2), 650, 90_000, MacroLibraryFileFormat.Json, isFavorite: false, lastUsedUtc: now.AddMinutes(-5))
        ];

        MacroLibraryViewItem[] favoriteItems = MacroLibraryFilterEngine.Apply(
            items,
            "json",
            MacroLibraryFilterKind.Favorites);
        MacroLibraryViewItem[] recentItems = MacroLibraryFilterEngine.Apply(
            items,
            null,
            MacroLibraryFilterKind.Recent);
        MacroLibraryViewItem[] longItems = MacroLibraryFilterEngine.Apply(
            items,
            null,
            MacroLibraryFilterKind.Long);
        MacroLibraryViewItem[] shortItems = MacroLibraryFilterEngine.Apply(
            items,
            "5000",
            MacroLibraryFilterKind.Short);

        Assert.Equal(
            ["FavoriteJson"],
            favoriteItems.Select(item => item.Entry.Name));
        Assert.Equal(
            ["RecentXml", "LongJson"],
            recentItems.Select(item => item.Entry.Name));
        Assert.Equal(
            ["LongJson"],
            longItems.Select(item => item.Entry.Name));
        Assert.Equal(
            ["FavoriteJson"],
            shortItems.Select(item => item.Entry.Name));

        return Task.CompletedTask;
    }
}
