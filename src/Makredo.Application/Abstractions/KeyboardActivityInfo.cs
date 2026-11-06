namespace Makredo.Application.Abstractions;

public sealed record KeyboardActivityInfo(
    int VirtualKeyCode,
    int ScanCode,
    bool IsKeyDown,
    bool IsExtendedKey,
    HotkeyModifiers ActiveModifiers,
    bool IsModifierKey,
    HotkeyModifiers ModifierKey,
    DateTime? TimestampUtc = null,
    bool IsInjected = false,
    ulong ExtraInfo = 0);
