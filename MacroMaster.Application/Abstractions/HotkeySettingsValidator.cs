namespace MacroMaster.Application.Abstractions;

public static class HotkeySettingsValidator
{
    private const HotkeyModifiers SupportedModifiers =
        HotkeyModifiers.Alt
        | HotkeyModifiers.Control
        | HotkeyModifiers.Shift
        | HotkeyModifiers.Windows;

    private static readonly HashSet<int> UnsupportedPrimaryVirtualKeySet =
    [
        0x01,
        0x02,
        0x04,
        0x05,
        0x06,
        0x10,
        0x11,
        0x12,
        0x5B,
        0x5C,
        0xA0,
        0xA1,
        0xA2,
        0xA3,
        0xA4,
        0xA5
    ];

    public static IReadOnlySet<int> UnsupportedPrimaryVirtualKeys => UnsupportedPrimaryVirtualKeySet;

    public static void Validate(
        HotkeySettings settings,
        string operationDescription)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string context = string.IsNullOrWhiteSpace(operationDescription)
            ? "Kisayol ayari dogrulamasi"
            : operationDescription;

        ValidateBinding(settings.RecordToggleHotkey, "Kaydi baslat/durdur kisayolu", context);
        ValidateBinding(settings.PlaybackToggleHotkey, "Oynat/duraklat kisayolu", context);
        ValidateBinding(settings.StopHotkey, "Durdurma kisayolu", context);
        ValidateBinding(settings.HotkeySettingsHotkey, "Kisayollari ac kisayolu", context);

        EnsureAllDistinct(
            context,
            ("Kaydi baslat/durdur kisayolu", settings.RecordToggleHotkey),
            ("Oynat/duraklat kisayolu", settings.PlaybackToggleHotkey),
            ("Durdurma kisayolu", settings.StopHotkey),
            ("Kisayollari ac kisayolu", settings.HotkeySettingsHotkey));
    }

    private static void ValidateBinding(
        HotkeyBinding? hotkeyBinding,
        string bindingName,
        string context)
    {
        if (hotkeyBinding is null)
        {
            throw CreateValidationException(
                context,
                $"{bindingName} zorunludur.");
        }

        if ((hotkeyBinding.Modifiers & ~SupportedModifiers) != 0)
        {
            throw CreateValidationException(
                context,
                $"{bindingName} desteklenmeyen degistirici bayraklari iceriyor: {hotkeyBinding.Modifiers}.");
        }

        if (hotkeyBinding.VirtualKeyCode <= 0 || hotkeyBinding.VirtualKeyCode > 0xFE)
        {
            throw CreateValidationException(
                context,
                $"{bindingName} gecerli bir sanal tus kodu kullanmalidir. Gecerli deger: {hotkeyBinding.VirtualKeyCode}.");
        }

        if (UnsupportedPrimaryVirtualKeySet.Contains(hotkeyBinding.VirtualKeyCode))
        {
            throw CreateValidationException(
                context,
                $"{bindingName} ana tus olarak fare dugmesi veya degistirici tus kullanamaz. Gecerli sanal tus: {hotkeyBinding.VirtualKeyCode}.");
        }
    }

    private static void EnsureAllDistinct(
        string context,
        params (string Name, HotkeyBinding Binding)[] bindings)
    {
        for (int firstIndex = 0; firstIndex < bindings.Length; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1; secondIndex < bindings.Length; secondIndex++)
            {
                if (bindings[firstIndex].Binding == bindings[secondIndex].Binding)
                {
                    throw CreateValidationException(
                        context,
                        $"{bindings[firstIndex].Name} ve {bindings[secondIndex].Name} ayni kisayolu kullanamaz ({Format(bindings[firstIndex].Binding)}).");
                }
            }
        }
    }

    private static InvalidOperationException CreateValidationException(
        string context,
        string message)
    {
        return new InvalidOperationException($"{context} basarisiz oldu. {message}");
    }

    private static string Format(HotkeyBinding hotkeyBinding)
    {
        List<string> parts = [];

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(VirtualKeyDisplayNameFormatter.Format(hotkeyBinding.VirtualKeyCode));
        return string.Join("+", parts);
    }
}
