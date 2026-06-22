using System.Runtime.InteropServices;

namespace Gideon.Interop;

/// <summary>
/// P/Invoke necesarios para: detectar la ventana en primer plano, convertir la ventana
/// del overlay en click-through / no-activable, y registrar hotkeys globales.
/// </summary>
internal static class NativeMethods
{
    // ---- Estilos extendidos de ventana ----
    public const int GWL_EXSTYLE = -20;

    public const int WS_EX_TRANSPARENT = 0x00000020; // click-through
    public const int WS_EX_LAYERED = 0x00080000; // necesario para transparencia por pixel
    public const int WS_EX_NOACTIVATE = 0x08000000; // no roba el foco
    public const int WS_EX_TOOLWINDOW = 0x00000080; // no aparece en Alt-Tab

    // ---- Hotkeys ----
    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    // Versiones 64-bit-safe de Get/SetWindowLong.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(nint hWnd, int id);
}
