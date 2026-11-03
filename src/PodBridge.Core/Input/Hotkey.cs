namespace PodBridge.Core.Input;

/// <summary>Modifier bits. The values match Win32 MOD_ALT, MOD_CONTROL, MOD_SHIFT and MOD_WIN.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A global hotkey: modifiers plus a Windows virtual-key code. Stored in settings as text like "Ctrl+Alt+A".</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, int VirtualKey)
{
    private static readonly (string Name, int Vk)[] NamedKeys =
    {
        ("Space", 0x20), ("Enter", 0x0D), ("Tab", 0x09),
        ("Left", 0x25), ("Up", 0x26), ("Right", 0x27), ("Down", 0x28),
        ("Insert", 0x2D), ("Delete", 0x2E), ("Home", 0x24), ("End", 0x23),
        ("PageUp", 0x21), ("PageDown", 0x22),
    };

    public static readonly Hotkey DefaultConnect = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'A');
    public static readonly Hotkey DefaultSwitch = new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 'S');

    /// <summary>
    /// Valid when the key is one we can name and the combination has Ctrl, Alt or Win
    /// (Shift alone would swallow normal typing).
    /// </summary>
    public bool IsValid =>
        (Modifiers & (HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Win)) != 0
        && KeyName(VirtualKey) is not null;

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
        parts.Add(KeyName(VirtualKey) ?? $"0x{VirtualKey:X2}");
        return string.Join("+", parts);
    }

    /// <summary>Parses text like "Ctrl+Alt+A". Returns false for anything that is not a valid hotkey.</summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var mods = HotkeyModifiers.None;
        int? vk = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0 || vk is not null)
            {
                return false; // empty part, or something after the key
            }
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= HotkeyModifiers.Control; break;
                case "alt": mods |= HotkeyModifiers.Alt; break;
                case "shift": mods |= HotkeyModifiers.Shift; break;
                case "win": case "windows": mods |= HotkeyModifiers.Win; break;
                default:
                    var code = ParseKey(raw);
                    if (code is null)
                    {
                        return false;
                    }
                    vk = code;
                    break;
            }
        }

        if (vk is null)
        {
            return false;
        }
        var candidate = new Hotkey(mods, vk.Value);
        if (!candidate.IsValid)
        {
            return false;
        }
        hotkey = candidate;
        return true;
    }

    /// <summary>
    /// Builds a hotkey from a key press in the capture box. Returns null while only a modifier is held
    /// or when the combination is not usable.
    /// </summary>
    public static Hotkey? FromKeyPress(int virtualKey, bool ctrl, bool alt, bool shift, bool win)
    {
        if (IsModifierKey(virtualKey))
        {
            return null;
        }
        var mods = HotkeyModifiers.None;
        if (ctrl) mods |= HotkeyModifiers.Control;
        if (alt) mods |= HotkeyModifiers.Alt;
        if (shift) mods |= HotkeyModifiers.Shift;
        if (win) mods |= HotkeyModifiers.Win;
        var h = new Hotkey(mods, virtualKey);
        return h.IsValid ? h : null;
    }

    public static bool IsModifierKey(int vk) =>
        vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);

    /// <summary>Readable name for a virtual-key code, or null if we do not support the key.</summary>
    public static string? KeyName(int vk)
    {
        if (vk is >= 'A' and <= 'Z' || vk is >= '0' and <= '9')
        {
            return ((char)vk).ToString();
        }
        if (vk is >= 0x70 and <= 0x87)
        {
            return "F" + (vk - 0x70 + 1);
        }
        if (vk is >= 0x60 and <= 0x69)
        {
            return "Num" + (vk - 0x60);
        }
        foreach (var (name, code) in NamedKeys)
        {
            if (code == vk)
            {
                return name;
            }
        }
        return null;
    }

    private static int? ParseKey(string name)
    {
        if (name.Length == 1 && (char.IsAsciiLetterOrDigit(name[0])))
        {
            return char.ToUpperInvariant(name[0]);
        }
        if (name.Length is 2 or 3 && (name[0] is 'F' or 'f') && int.TryParse(name.AsSpan(1), out int f) && f is >= 1 and <= 24)
        {
            return 0x70 + f - 1;
        }
        if (name.StartsWith("Num", StringComparison.OrdinalIgnoreCase) && name.Length == 4
            && char.IsAsciiDigit(name[3]))
        {
            return 0x60 + (name[3] - '0');
        }
        foreach (var (n, code) in NamedKeys)
        {
            if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
            {
                return code;
            }
        }
        return null;
    }
}
