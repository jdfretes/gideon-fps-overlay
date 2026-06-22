using Gideon.Interop;

namespace Gideon.Services;

/// <summary>
/// Devuelve el PID de la ventana actualmente en primer plano (el juego activo).
/// </summary>
internal static class ForegroundService
{
    /// <summary>
    /// PID de la ventana en foco, o 0 si no se puede determinar.
    /// </summary>
    public static uint GetForegroundProcessId()
    {
        nint hWnd = NativeMethods.GetForegroundWindow();
        if (hWnd == 0)
            return 0;

        _ = NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
        return pid;
    }
}
