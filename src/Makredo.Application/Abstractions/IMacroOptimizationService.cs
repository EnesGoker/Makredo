using Makredo.Domain.Models;

namespace Makredo.Application.Abstractions;

public interface IMacroOptimizationService
{
    MacroOptimizationPreview Preview(MacroSession session);
}
