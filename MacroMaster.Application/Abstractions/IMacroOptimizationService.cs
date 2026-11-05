using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public interface IMacroOptimizationService
{
    MacroOptimizationPreview Preview(MacroSession session);
}
