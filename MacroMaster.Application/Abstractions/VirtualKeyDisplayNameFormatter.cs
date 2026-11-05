namespace MacroMaster.Application.Abstractions;

public static class VirtualKeyDisplayNameFormatter
{
    public static string Format(int virtualKeyCode)
    {
        return virtualKeyCode switch
        {
            >= 0x30 and <= 0x39 => ((char)virtualKeyCode).ToString(),
            >= 0x41 and <= 0x5A => ((char)virtualKeyCode).ToString(),
            >= 0x60 and <= 0x69 => $"NumPad {virtualKeyCode - 0x60}",
            >= 0x70 and <= 0x87 => $"F{virtualKeyCode - 0x6F}",
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x13 => "Pause",
            0x14 => "Caps Lock",
            0x1B => "Esc",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2C => "Print Screen",
            0x2D => "Insert",
            0x2E => "Delete",
            0x6A => "NumPad *",
            0x6B => "NumPad +",
            0x6D => "NumPad -",
            0x6E => "NumPad .",
            0x6F => "NumPad /",
            0x90 => "Num Lock",
            0x91 => "Scroll Lock",
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => $"VK {virtualKeyCode}"
        };
    }
}
