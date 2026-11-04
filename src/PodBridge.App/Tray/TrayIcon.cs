using System.Runtime.InteropServices;
using PodBridge.App.Native;
using PodBridge.Core.Audio;
using static PodBridge.App.Native.NativeMethods;

namespace PodBridge.App.Tray;

/// <summary>
/// A notification-area icon built directly on Shell_NotifyIcon. It owns a hidden window that receives
/// the icon's mouse messages, and shows a native pop-up menu on right-click.
/// Must be created and used on the UI thread.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint CallbackMessage = WM_APP + 1;
    private const uint IconId = 1;
    private const int CmdOpen = 1, CmdSettings = 2, CmdExit = 3, CmdToggle = 4, CmdSwitch = 6;
    private const string ClassName = "PodBridgeTrayWindow";

    private readonly WndProc _wndProc;   // field keeps the delegate alive for the native window
    private readonly uint _taskbarCreatedMessage;
    private IntPtr _hwnd;
    private IntPtr _hicon;
    private string _tooltip = "PodBridge";
    private bool _added;
    private bool _disposed;

    public event Action? OpenRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;
    public event Action? ConnectRequested;
    public event Action? DisconnectRequested;
    public event Action? SwitchOutputRequested;

    /// <summary>Supplies the connection state and the label of the single Connect / Disconnect item when the menu opens.</summary>
    public Func<(bool Connected, ToggleView View)>? ConnectionToggleProvider { get; set; }

    /// <summary>Raised for WM_HOTKEY; the argument is the hotkey id passed to RegisterHotKey.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>The hidden window, which is also the target of RegisterHotKey.</summary>
    public IntPtr Handle => _hwnd;

    public TrayIcon()
    {
        _wndProc = WindowProc;
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

        var hInstance = GetModuleHandleW(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = _wndProc,
            hInstance = hInstance,
            lpszClassName = ClassName,
        };
        RegisterClassExW(ref wc); // fails harmlessly with "class exists" if called twice

        // A normal (never shown) top-level window, not a message-only one,
        // because only top-level windows receive the "TaskbarCreated" broadcast.
        _hwnd = CreateWindowExW(0, ClassName, "PodBridge tray", 0, 0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not create the tray message window.");
        }
    }

    /// <summary>Adds the icon to the tray, or updates it if already there.</summary>
    public void Update(IntPtr newIcon, string tooltip)
    {
        if (_disposed)
        {
            DestroyIcon(newIcon);
            return;
        }

        var old = _hicon;
        _hicon = newIcon;
        _tooltip = tooltip.Length > 127 ? tooltip[..127] : tooltip;

        var data = CreateData();
        if (!_added)
        {
            _added = Shell_NotifyIconW(NIM_ADD, ref data);
        }
        else
        {
            Shell_NotifyIconW(NIM_MODIFY, ref data);
        }

        if (old != IntPtr.Zero)
        {
            DestroyIcon(old);
        }
    }

    private NOTIFYICONDATA CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
        uCallbackMessage = CallbackMessage,
        hIcon = _hicon,
        szTip = _tooltip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == CallbackMessage)
            {
                switch ((uint)(lParam.ToInt64() & 0xFFFF))
                {
                    case WM_LBUTTONUP:
                    case WM_LBUTTONDBLCLK:
                        OpenRequested?.Invoke();
                        break;
                    case WM_RBUTTONUP:
                        ShowMenu();
                        break;
                }
                return IntPtr.Zero;
            }

            if (msg == WM_HOTKEY)
            {
                HotkeyPressed?.Invoke((int)wParam.ToInt64());
                return IntPtr.Zero;
            }

            if (msg == _taskbarCreatedMessage && !_disposed)
            {
                // Explorer restarted: the icon vanished, so add it again.
                _added = false;
                var data = CreateData();
                _added = Shell_NotifyIconW(NIM_ADD, ref data);
                return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Tray window procedure failed", ex);
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        try
        {
            AppendMenuW(menu, MF_STRING, new UIntPtr(CmdOpen), "Open PodBridge");
            AppendMenuW(menu, MF_STRING, new UIntPtr(CmdSettings), "Settings");
            AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            bool connected = false;
            var toggle = new ToggleView("Connect AirPods", true, true);
            try
            {
                if (ConnectionToggleProvider is { } provider)
                {
                    (connected, toggle) = provider();
                    toggle = toggle with { Label = toggle.Label.EndsWith('…') ? toggle.Label : $"{toggle.Label} AirPods" };
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not read the connection state for the tray menu", ex);
            }
            AppendMenuW(menu, MF_STRING | (toggle.Enabled ? 0u : MF_GRAYED), new UIntPtr(CmdToggle), toggle.Label);
            AppendMenuW(menu, MF_STRING, new UIntPtr(CmdSwitch), "Switch output (AirPods / speakers)");
            AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            AppendMenuW(menu, MF_STRING, new UIntPtr(CmdExit), "Exit");

            GetCursorPos(out var pt);
            // Required so the menu closes when the user clicks elsewhere.
            SetForegroundWindow(_hwnd);
            int command = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN,
                pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
            PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case CmdOpen: OpenRequested?.Invoke(); break;
                case CmdSettings: SettingsRequested?.Invoke(); break;
                case CmdToggle:
                    if (connected) { DisconnectRequested?.Invoke(); } else { ConnectRequested?.Invoke(); }
                    break;
                case CmdSwitch: SwitchOutputRequested?.Invoke(); break;
                case CmdExit: ExitRequested?.Invoke(); break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_added)
        {
            var data = CreateData();
            Shell_NotifyIconW(NIM_DELETE, ref data);
            _added = false;
        }
        if (_hicon != IntPtr.Zero)
        {
            DestroyIcon(_hicon);
            _hicon = IntPtr.Zero;
        }
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
            UnregisterClassW(ClassName, GetModuleHandleW(null));
        }
    }
}
