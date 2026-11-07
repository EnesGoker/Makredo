using Makredo.Application.Abstractions;

namespace Makredo.Application.Hotkeys;

public sealed class HotkeyRecordingFilter(IEnumerable<HotkeyBinding> commands, IEnumerable<int>? initiallyHeldKeys = null)
{
    private readonly HotkeyBinding[] _commands = commands.Distinct().ToArray();
    private readonly HashSet<int> _suppressed = initiallyHeldKeys is null ? [] : new(initiallyHeldKeys);
    private readonly List<KeyboardActivityInfo> _pending = [];

    public IReadOnlyList<KeyboardActivityInfo> Process(KeyboardActivityInfo input)
    {
        if (_suppressed.Contains(input.VirtualKeyCode))
        {
            if (!input.IsKeyDown)
            {
                _suppressed.Remove(input.VirtualKeyCode);
                return [];
            }
            if (ModifierForKey(input.VirtualKeyCode) == HotkeyModifiers.None
                && _commands.Any(binding => binding.VirtualKeyCode == input.VirtualKeyCode && binding.Modifiers == input.ActiveModifiers))
            {
                return [];
            }
            _suppressed.Remove(input.VirtualKeyCode);
        }
        HotkeyModifiers modifier = ModifierForKey(input.VirtualKeyCode);
        if (modifier != HotkeyModifiers.None)
        {
            _pending.Add(input);
            return input.IsKeyDown ? [] : Flush();
        }
        HotkeyBinding? command = _commands.FirstOrDefault(binding => binding.VirtualKeyCode == input.VirtualKeyCode && binding.Modifiers == input.ActiveModifiers);
        if (command is not null)
        {
            List<KeyboardActivityInfo> preserved = [];
            foreach (KeyboardActivityInfo pending in _pending)
            {
                if (pending.IsKeyDown && (ModifierForKey(pending.VirtualKeyCode) & command.Modifiers) != 0)
                {
                    _suppressed.Add(pending.VirtualKeyCode);
                }
                else { preserved.Add(pending); }
            }
            _pending.Clear();
            if (input.IsKeyDown) { _suppressed.Add(input.VirtualKeyCode); }
            return preserved;
        }
        var result = new List<KeyboardActivityInfo>(_pending) { input };
        _pending.Clear();
        return result;
    }

    public IReadOnlyList<KeyboardActivityInfo> Flush()
    {
        KeyboardActivityInfo[] result = _pending.ToArray();
        _pending.Clear();
        return result;
    }

    public static HotkeyModifiers ModifierForKey(int key) => key switch
    {
        0x10 or 0xA0 or 0xA1 => HotkeyModifiers.Shift,
        0x11 or 0xA2 or 0xA3 => HotkeyModifiers.Control,
        0x12 or 0xA4 or 0xA5 => HotkeyModifiers.Alt,
        0x5B or 0x5C => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None
    };
}
