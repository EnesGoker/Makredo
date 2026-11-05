using Makredo.Domain.Models;

namespace Makredo.WinForms.Controls;

internal sealed record EventListViewItem(
    int SourceIndex,
    MacroEvent Event,
    int ElapsedMs);
