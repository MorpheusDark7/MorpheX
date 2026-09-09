using System.Runtime.InteropServices;
using System.Windows.Input;
using MorpheX.Core.Configuration;
using MorpheX.Core.Services;
using Serilog;

namespace MorpheX.Services;

public interface IHotkeyService : IDisposable
{
    void Start();
    void Stop();
    void UpdateBindings();
}

public sealed class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const int HWND_MESSAGE = -3;

    private const int HOTKEY_ID_PAUSE_RESUME = 9001;
    private const int HOTKEY_ID_MUTE_UNMUTE = 9002;
    private const int HOTKEY_ID_NEXT_WALLPAPER = 9003;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private readonly ISettingsService _settingsService;
    private readonly IPlaybackService _playbackService;
    private readonly IWallpaperService _wallpaperService;
    private readonly IPlaylistService _playlistService;

    private IntPtr _hwnd = IntPtr.Zero;
    private WndProc? _wndProcDelegate;
    private bool _disposed;

    private readonly HashSet<int> _registeredIds = new();

    public HotkeyService(
        ISettingsService settingsService,
        IPlaybackService playbackService,
        IWallpaperService wallpaperService,
        IPlaylistService playlistService)
    {
        _settingsService = settingsService;
        _playbackService = playbackService;
        _wallpaperService = wallpaperService;
        _playlistService = playlistService;
    }

    public void Start()
    {
        if (_hwnd != IntPtr.Zero) return;

        CreateMessageWindow();
        UpdateBindings();
        Log.Information("HotkeyService started");
    }

    public void Stop()
    {
        UnregisterAll();
        DestroyMessageWindow();
        Log.Debug("HotkeyService stopped");
    }

    public void UpdateBindings()
    {
        if (_hwnd == IntPtr.Zero) return;

        UnregisterAll();

        var hotkeys = _settingsService.Settings.Hotkeys;

        RegisterSingleBinding(HOTKEY_ID_PAUSE_RESUME, hotkeys.PauseResume, "Pause/Resume");
        RegisterSingleBinding(HOTKEY_ID_MUTE_UNMUTE, hotkeys.MuteUnmute, "Mute/Unmute");
        RegisterSingleBinding(HOTKEY_ID_NEXT_WALLPAPER, hotkeys.NextWallpaper, "Next Wallpaper");
    }

    private void RegisterSingleBinding(int id, HotkeyBinding binding, string actionName)
    {
        if (!binding.IsConfigured) return;

        if (TryParseBinding(binding, out uint modifiers, out uint vk))
        {
            modifiers |= MOD_NOREPEAT;

            bool success = RegisterHotKey(_hwnd, id, modifiers, vk);
            if (success)
            {
                _registeredIds.Add(id);
                Log.Information("Registered global hotkey for '{Action}': {Binding}",
                    actionName, binding.DisplayText);
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                Log.Warning("Failed to register hotkey for '{Action}' ({Binding}). Error: {Error}. The shortcut may be used by another app.",
                    actionName, binding.DisplayText, error);
            }
        }
    }

    private void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero) return;

        foreach (int id in _registeredIds)
        {
            UnregisterHotKey(_hwnd, id);
        }
        _registeredIds.Clear();
    }

    private void HandleHotkey(int id)
    {
        try
        {
            switch (id)
            {
                case HOTKEY_ID_PAUSE_RESUME:
                    Log.Information("Global Hotkey: Toggle Pause/Resume");
                    if (_playbackService.IsManuallyPaused)
                    {
                        _playbackService.Resume();
                    }
                    else
                    {
                        _playbackService.Pause();
                    }
                    break;

                case HOTKEY_ID_MUTE_UNMUTE:
                    Log.Information("Global Hotkey: Toggle Audio Mute");
                    var audio = _settingsService.Settings.Audio;
                    audio.Enabled = !audio.Enabled;
                    if (!audio.Enabled)
                    {
                        _wallpaperService.SetVolume(-1);
                    }
                    else
                    {
                        _wallpaperService.SetVolume(audio.Volume / 100f);
                    }
                    _ = _settingsService.SaveAsync();
                    break;

                case HOTKEY_ID_NEXT_WALLPAPER:
                    Log.Information("Global Hotkey: Next Wallpaper");
                    _ = _playlistService.TriggerNextWallpaperAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error executing global hotkey action (id: {Id})", id);
        }
    }

    public static bool TryParseBinding(HotkeyBinding binding, out uint fsModifiers, out uint virtualKey)
    {
        fsModifiers = 0;
        virtualKey = 0;

        if (!binding.IsConfigured) return false;

        var modParts = binding.Modifiers.Split(new[] { '+', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var mod in modParts)
        {
            switch (mod.Trim().ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    fsModifiers |= MOD_CONTROL;
                    break;
                case "alt":
                    fsModifiers |= MOD_ALT;
                    break;
                case "shift":
                    fsModifiers |= MOD_SHIFT;
                    break;
                case "win":
                case "windows":
                    fsModifiers |= MOD_WIN;
                    break;
            }
        }

        if (Enum.TryParse<Key>(binding.Key, true, out var wpfKey))
        {
            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
            return virtualKey > 0;
        }

        return false;
    }

    #region Win32 Message-Only Window
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private void CreateMessageWindow()
    {
        string className = "MorpheX_Hotkey_Receiver_" + Guid.NewGuid().ToString("N");
        _wndProcDelegate = CustomWndProc;

        var wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = GetModuleHandle(null),
            lpszClassName = className
        };

        ushort atom = RegisterClassEx(ref wndClass);
        if (atom == 0)
        {
            Log.Warning("Failed to register hotkey message window class. Error: {Error}", Marshal.GetLastWin32Error());
            return;
        }

        _hwnd = CreateWindowEx(
            0,
            className,
            "MorpheX Hotkey Sink",
            0,
            0, 0, 0, 0,
            (IntPtr)HWND_MESSAGE,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            Log.Warning("Failed to create hotkey message-only window. Error: {Error}", Marshal.GetLastWin32Error());
        }
    }

    private void DestroyMessageWindow()
    {
        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            HandleHotkey(id);
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    #endregion
}
