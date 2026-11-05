using MacroMaster.Domain.Models;

namespace MacroMaster.WinForms.Controls;

internal sealed record EventListViewItem(
    int SourceIndex,
    MacroEvent Event,
    int ElapsedMs);
