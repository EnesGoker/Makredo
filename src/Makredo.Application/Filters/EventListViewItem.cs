using Makredo.Domain.Models;

namespace Makredo.Application.Filters;

internal sealed record EventListViewItem(
    int SourceIndex,
    MacroEvent Event,
    int ElapsedMs);
