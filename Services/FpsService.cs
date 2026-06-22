using PresentMonFps;

namespace Gideon.Services;

/// <summary>
/// Envuelve PresentMonFps para medir, de forma pasiva (ETW, sin inyeccion), los FPS de
/// un proceso concreto. Solo hay una sesion de trazado ETW activa a la vez: cuando cambia
/// el proceso objetivo se cancela el bucle anterior y se arranca uno nuevo.
/// </summary>
internal sealed class FpsService : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private uint _currentPid;
    private double? _currentFps;

    /// <summary>Ultimo valor de FPS medido, o null si no hay datos para el proceso en foco.</summary>
    public double? CurrentFps { get { lock (_gate) return _currentFps; } }

    /// <summary>True si PresentMonFps puede operar (Windows + admin).</summary>
    public static bool IsAvailable => FpsInspector.IsAvailable;

    public static bool IsRunAsAdmin => FpsInspector.IsRunAsAdmin();

    /// <summary>
    /// Empieza (o cambia) la medicion al PID indicado. Si ya estabamos midiendo ese PID
    /// no hace nada. PID 0 detiene la medicion.
    /// </summary>
    public async Task TrackAsync(uint pid)
    {
        if (pid == _currentPid)
            return;

        await StopAsync().ConfigureAwait(false);

        if (pid == 0)
            return;

        CancellationTokenSource cts = new();
        FpsRequest request = new(pid) { PeriodMillisecond = 500 };

        // Crear el Task antes de adquirir el lock para evitar iniciar trabajo dentro de el.
        var newLoop = Task.Run(async () =>
        {
            try
            {
                await FpsInspector.StartForeverAsync(request, OnFps, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch
            {
                lock (_gate) _currentFps = null;
            }
        });

        lock (_gate)
        {
            _currentPid = pid;
            _cts = cts;
            _loop = newLoop;
        }
    }

    /// <summary>Detiene la medicion actual y libera la sesion ETW.</summary>
    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_gate)
        {
            cts = _cts;
            loop = _loop;
            _cts = null;
            _loop = null;
            _currentPid = 0;
        }

        if (cts is not null)
        {
            try { cts.Cancel(); } catch { /* ignore */ }
        }

        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); } catch { /* ignore */ }
        }

        try { FpsInspector.StopTraceSession(); } catch { }

        cts?.Dispose();
        lock (_gate) _currentFps = null;
    }

    private void OnFps(FpsResult result)
    {
        lock (_gate) _currentFps = result.IsCanceled ? null : result.Fps;
    }

    public void Dispose()
    {
        // Bloqueante a proposito: se llama al cerrar la app.
        StopAsync().GetAwaiter().GetResult();
    }
}
