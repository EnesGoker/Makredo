using MacroMaster.Application.Abstractions;

namespace MacroMaster.WinForms.Controls;

internal sealed record MacroLibraryViewItem(
    MacroLibraryEntry Entry,
    bool IsFavorite,
    DateTime? LastUsedUtc);
