using MacroMaster.Domain.Enums;

namespace MacroMaster.Domain.Models;

public sealed class MacroEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public MacroEventType EventType { get; set; } = MacroEventType.System;

    public KeyboardActionType KeyboardActionType { get; set; } = KeyboardActionType.None;

    public MouseActionType MouseActionType { get; set; } = MouseActionType.None;

    public int DelayMs { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public int? KeyCode { get; set; }

    public int? ScanCode { get; set; }

    public bool IsExtendedKey { get; set; }

    public string? KeyName { get; set; }

    public int? X { get; set; }

    public int? Y { get; set; }

    public int? WheelDelta { get; set; }

    public string Description { get; set; } = string.Empty;
}
