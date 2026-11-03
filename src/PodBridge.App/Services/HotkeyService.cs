using System.Runtime.InteropServices;
using PodBridge.App.Native;
using PodBridge.Core.Input;
using PodBridge.Core.Settings;

namespace PodBridge.App.Services;

internal enum HotkeyAction
{
    ToggleConnection,
    SwitchOutput,
}

/// <summary>
/// Global hotkeys through RegisterHotKey, on the tray's hidden window. Messages arrive on the UI thread.
/// A shortcut that Windows or another program already owns fails to register; the reason is kept so Settings can show it.
/// </summary>
internal sealed class HotkeyService : IDisposable
{
    private const int ConnectId = 0x5042;
    private const int SwitchId = 0x5043;

    private readonly IntPtr _hwnd;
    private bool _connectRegistered;
    private bool _switchRegistered;

    public HotkeyService(IntPtr hwnd) => _hwnd = hwnd;

    public event Action<HotkeyAction>? Pressed;

    /// <summary>Why the connect shortcut is not active, or null if it is active or switched off.</summary>
    public string? ConnectStatus { get; private set; }

    /// <summary>Why the switch-output shortcut is not active, or null if it is active or switched off.</summary>
    public string? SwitchStatus { get; private set; }

    /// <summary>Registers (or unregisters) the shortcuts to match the settings.</summary>
    public void Apply(AppSettings settings)
    {
        Unregister();
        ConnectStatus = null;
        SwitchStatus = null;
        if (!settings.HotkeysEnabled)
        {
            return;
        }

        Hotkey.TryParse(settings.ConnectHotkey, out var connect);
        Hotkey.TryParse(settings.SwitchHotkey, out var change);
        bool hasConnect = connect.IsValid;
        bool hasSwitch = change.IsValid;

        if (hasConnect)
        {
            _connectRegistered = Register(ConnectId, connect, out var error);
            ConnectStatus = error;
        }
        if (hasSwitch)
        {
            if (hasConnect && connect == change)
            {
                SwitchStatus = "Same shortcut as the other action. Pick a different one.";
            }
            else
            {
                _switchRegistered = Register(SwitchId, change, out var error);
                SwitchStatus = error;
            }
        }
    }

    /// <summary>Called by the tray window for every WM_HOTKEY message.</summary>
    public void HandleHotkeyMessage(int id)
    {
        if (id == ConnectId && _connectRegistered)
        {
            Pressed?.Invoke(HotkeyAction.ToggleConnection);
        }
        else if (id == SwitchId && _switchRegistered)
        {
            Pressed?.Invoke(HotkeyAction.SwitchOutput);
        }
    }

    private bool Register(int id, Hotkey hotkey, out string? error)
    {
        // MOD_NOREPEAT: holding the keys down must not fire the action again and again.
        if (NativeMethods.RegisterHotKey(_hwnd, id, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)hotkey.VirtualKey))
        {
            error = null;
            return true;
        }

        int code = Marshal.GetLastWin32Error();
        error = code == NativeMethods.ERROR_HOTKEY_ALREADY_REGISTERED
            ? $"{hotkey} is already used by Windows or another program. Pick a different shortcut."
            : $"Windows could not register {hotkey} (error {code}).";
        return false;
    }

    private void Unregister()
    {
        if (_connectRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, ConnectId);
            _connectRegistered = false;
        }
        if (_switchRegistered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, SwitchId);
            _switchRegistered = false;
        }
    }

    public void Dispose() => Unregister();
}
