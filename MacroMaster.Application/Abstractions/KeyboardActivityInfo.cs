namespace MacroMaster.Application.Abstractions;

public sealed record KeyboardActivityInfo(
    int VirtualKeyCode,
    int ScanCode,
    bool IsKeyDown,
    bool IsExtendedKey,
    HotkeyModifiers ActiveModifiers,
    bool IsModifierKey,
    HotkeyModifiers ModifierKey);
