namespace MacroMaster.Application.Abstractions;

public sealed record HotkeyBinding(
    int VirtualKeyCode,
    HotkeyModifiers Modifiers)
{
    public static HotkeyBinding None(int virtualKeyCode) => new(virtualKeyCode, HotkeyModifiers.None);
}
