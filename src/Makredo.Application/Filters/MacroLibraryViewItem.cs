using Makredo.Application.Abstractions;

namespace Makredo.Application.Filters;

internal sealed record MacroLibraryViewItem(
    MacroLibraryEntry Entry,
    bool IsFavorite,
    DateTime? LastUsedUtc);
