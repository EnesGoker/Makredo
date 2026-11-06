using Makredo.Domain.Enums;

namespace Makredo.Application.Abstractions;

public sealed record MouseActivityInfo(
    MouseActionType ActionType,
    int? X,
    int? Y,
    int? WheelDelta,
    DateTime TimestampUtc);
