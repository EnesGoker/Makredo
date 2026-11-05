using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public sealed record MacroOptimizationPreview(
    string SessionName,
    int OriginalEventCount,
    int OptimizedEventCount,
    int RemovedEventCount,
    int OriginalDurationMs,
    int OptimizedDurationMs,
    IReadOnlyList<MacroEvent> OptimizedEvents)
{
    public bool HasChanges => RemovedEventCount > 0;

    public double ReductionPercent => OriginalEventCount <= 0
        ? 0
        : RemovedEventCount * 100d / OriginalEventCount;

    public bool PreservesDuration => OriginalDurationMs == OptimizedDurationMs;
}
