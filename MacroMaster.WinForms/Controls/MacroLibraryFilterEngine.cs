using MacroMaster.Application.Abstractions;
using System.Globalization;

namespace MacroMaster.WinForms.Controls;

internal static class MacroLibraryFilterEngine
{
    internal const int ShortMacroMaxEventCount = 100;
    internal const int ShortMacroMaxDurationMs = 30_000;
    internal const int LongMacroMinEventCount = 500;
    internal const int LongMacroMinDurationMs = 60_000;

    public static MacroLibraryViewItem[] Apply(
        IEnumerable<MacroLibraryViewItem> items,
        string? searchTerm,
        MacroLibraryFilterKind filterKind)
    {
        ArgumentNullException.ThrowIfNull(items);

        string[] searchTokens = Tokenize(searchTerm);

        IEnumerable<MacroLibraryViewItem> query = items
            .Where(item => MatchesSearch(item, searchTokens))
            .Where(item => MatchesFilter(item, filterKind));

        query = filterKind == MacroLibraryFilterKind.Recent
            ? query
                .OrderByDescending(item => item.LastUsedUtc ?? DateTime.MinValue)
                .ThenBy(item => item.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
            : query
                .OrderByDescending(item => item.Entry.LastModifiedUtc)
                .ThenBy(item => item.Entry.Name, StringComparer.CurrentCultureIgnoreCase);

        return query.ToArray();
    }

    private static bool MatchesFilter(
        MacroLibraryViewItem item,
        MacroLibraryFilterKind filterKind)
    {
        return filterKind switch
        {
            MacroLibraryFilterKind.All => true,
            MacroLibraryFilterKind.Favorites => item.IsFavorite,
            MacroLibraryFilterKind.Recent => item.LastUsedUtc.HasValue,
            MacroLibraryFilterKind.Json => item.Entry.Format == MacroLibraryFileFormat.Json,
            MacroLibraryFilterKind.Xml => item.Entry.Format == MacroLibraryFileFormat.Xml,
            MacroLibraryFilterKind.Short => IsShortMacro(item),
            MacroLibraryFilterKind.Long => IsLongMacro(item),
            _ => true
        };
    }

    private static bool IsShortMacro(MacroLibraryViewItem item)
    {
        return item.Entry.EventCount <= ShortMacroMaxEventCount
            && item.Entry.TotalDurationMs <= ShortMacroMaxDurationMs;
    }

    private static bool IsLongMacro(MacroLibraryViewItem item)
    {
        return item.Entry.EventCount >= LongMacroMinEventCount
            || item.Entry.TotalDurationMs >= LongMacroMinDurationMs;
    }

    private static bool MatchesSearch(
        MacroLibraryViewItem item,
        string[] searchTokens)
    {
        if (searchTokens.Length == 0)
        {
            return true;
        }

        string[] searchableValues =
        [
            item.Entry.Name,
            Path.GetFileName(item.Entry.FilePath),
            FormatFileFormat(item.Entry.Format),
            item.Entry.EventCount.ToString(CultureInfo.InvariantCulture),
            item.Entry.TotalDurationMs.ToString(CultureInfo.InvariantCulture),
            FormatDuration(item.Entry.TotalDurationMs),
            item.IsFavorite ? "favori" : string.Empty
        ];

        return searchTokens.All(token => searchableValues.Any(value =>
            value.Contains(token, StringComparison.CurrentCultureIgnoreCase)));
    }

    private static string[] Tokenize(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return [];
        }

        return searchTerm
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }

    private static string FormatFileFormat(MacroLibraryFileFormat format)
    {
        return format switch
        {
            MacroLibraryFileFormat.Json => "json",
            MacroLibraryFileFormat.Xml => "xml",
            _ => format.ToString().ToLowerInvariant()
        };
    }

    private static string FormatDuration(int totalDurationMs)
    {
        if (totalDurationMs < 1_000)
        {
            return totalDurationMs.ToString(CultureInfo.InvariantCulture) + " ms";
        }

        double totalSeconds = totalDurationMs / 1_000d;
        return totalSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " sn";
    }
}
