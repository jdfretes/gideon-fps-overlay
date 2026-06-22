using System.Diagnostics;

namespace Gideon.Services;

/// <summary>
/// Gestiona el arranque automático con Windows via Task Scheduler.
/// Se usa schtasks en vez del registro porque la app corre elevada y
/// /rl highest lanza la tarea con admin al arrancar sin mostrar UAC.
/// </summary>
public static class StartupService
{
    private const string TaskName = "Gideon FPS Overlay";

    public static bool IsEnabled()
    {
        try
        {
            using var p = Run("schtasks", $"/query /tn \"{TaskName}\"", capture: true);
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    public static void Enable()
    {
        string exe = GetExePath();
        if (string.IsNullOrEmpty(exe)) return;
        Run("schtasks",
            $"/create /tn \"{TaskName}\" /tr \"\\\"{exe}\\\"\" /sc onlogon /rl highest /f");
    }

    public static void Disable() =>
        Run("schtasks", $"/delete /tn \"{TaskName}\" /f");

    private static Process? Run(string cmd, string args, bool capture = false)
    {
        var psi = new ProcessStartInfo(cmd, args)
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
        };
        var p = Process.Start(psi);
        p?.WaitForExit();
        return p;
    }

    private static string GetExePath() =>
        Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
}
