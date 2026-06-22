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

    /// <summary>Ultimo valor de FPS medido, o null si no hay datos para el proceso en foco.</summary>
    public double? CurrentFps { get; private set; }

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
        lock (_gate)
        {
            _currentPid = pid;
            _cts = cts;
        }

        FpsRequest request = new(pid) { PeriodMillisecond = 500 };

        _loop = Task.Run(async () =>
        {
            try
            {
                await FpsInspector.StartForeverAsync(request, OnFps, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancelacion esperada al cambiar de proceso.
            }
            catch (Exception)
            {
                // El proceso no presenta frames (no es una app grafica) o termino. Sin datos.
                CurrentFps = null;
            }
        });
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

        try { FpsInspector.StopTraceSession(); } catch { /* ignore */ }

        cts?.Dispose();
        CurrentFps = null;
    }

    private void OnFps(FpsResult result)
    {
        CurrentFps = result.IsCanceled ? null : result.Fps;
    }

    public void Dispose()
    {
        // Bloqueante a proposito: se llama al cerrar la app.
        StopAsync().GetAwaiter().GetResult();
    }
}
