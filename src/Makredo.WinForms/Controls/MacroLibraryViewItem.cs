using Makredo.Application.Abstractions;

namespace Makredo.WinForms.Controls;

internal sealed record MacroLibraryViewItem(
    MacroLibraryEntry Entry,
    bool IsFavorite,
    DateTime? LastUsedUtc);
