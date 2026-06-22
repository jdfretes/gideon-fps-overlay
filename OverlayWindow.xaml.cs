using System.Globalization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Gideon.Interop;
using Gideon.Services;

namespace Gideon;

public partial class OverlayWindow : Window
{
    private const int HotkeyToggle = 1;
    private const int HotkeyQuit = 2;
    private const uint VkF = 0x46; // tecla F
    private const uint VkQ = 0x51; // tecla Q

    private readonly FpsService _fps = new();
    private readonly DispatcherTimer _timer;
    private readonly uint _ownPid = (uint)Environment.ProcessId;

    private uint _lastRequestedPid;
    private bool _switching;

    private static readonly System.Windows.Media.SolidColorBrush BrushGood =
        new(System.Windows.Media.Color.FromRgb(109, 200, 0));    // verde lima  ≥60  (#6DC800)
    private static readonly System.Windows.Media.SolidColorBrush BrushWarn =
        new(System.Windows.Media.Color.FromRgb(245, 200, 0));    // amarillo    30–59 (#F5C800)
    private static readonly System.Windows.Media.SolidColorBrush BrushBad =
        new(System.Windows.Media.Color.FromRgb(239, 68, 68));    // rojo        <30

    public Corner CurrentCorner { get; private set; } = Corner.TopRight;

    /// <summary>Inicializa la ventana (crea el handle, registra hotkeys) pero la deja oculta.</summary>
    public void ShowInvisible()
    {
        Show();
        Hide();
    }

    public void SetCorner(Corner corner)
    {
        CurrentCorner = corner;
        Reposition();
    }

    // Recalcula Left/Top usando las dimensiones reales del momento.
    // Se llama desde SetCorner y desde SizeChanged para que al cambiar de
    // 1 a 2 o 3 dígitos la ventana no se salga de la pantalla.
    private void Reposition()
    {
        if (!IsLoaded) return;

        Rect screen = SystemParameters.WorkArea;
        const double margin = 16;
        double w = ActualWidth;
        double h = ActualHeight;

        (Left, Top) = CurrentCorner switch
        {
            Corner.TopRight    => (screen.Right  - w - margin, screen.Top    + margin),
            Corner.BottomLeft  => (screen.Left   + margin,     screen.Bottom - h - margin),
            Corner.BottomRight => (screen.Right  - w - margin, screen.Bottom - h - margin),
            _                  => (screen.Left   + margin,     screen.Top    + margin),
        };
    }

    public OverlayWindow(Corner initialCorner = Corner.TopRight)
    {
        InitializeComponent();
        CurrentCorner = initialCorner;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300),
        };
        _timer.Tick += OnTick;

        // Reposicionar cada vez que el tamaño cambie (dígitos distintos = ancho distinto).
        SizeChanged += (_, _) => Reposition();
        Loaded      += (_, _) => Reposition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        nint hWnd = new WindowInteropHelper(this).Handle;

        // Convertir en click-through, no-activable y fuera de Alt-Tab.
        nint ex = NativeMethods.GetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TRANSPARENT |
              NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hWnd, NativeMethods.GWL_EXSTYLE, ex);

        // Hotkeys globales: Ctrl+Alt+F (mostrar/ocultar), Ctrl+Alt+Q (salir).
        uint mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
        NativeMethods.RegisterHotKey(hWnd, HotkeyToggle, mods, VkF);
        NativeMethods.RegisterHotKey(hWnd, HotkeyQuit, mods, VkQ);

        HwndSource.FromHwnd(hWnd)?.AddHook(WndProc);

        _timer.Start();
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            switch ((int)wParam)
            {
                case HotkeyToggle:
                    if (IsVisible) Hide(); else Show();
                    Services.SettingsService.Save(new Services.AppSettings
                    {
                        Corner = CurrentCorner,
                        OverlayVisible = IsVisible,
                    });
                    handled = true;
                    break;
                case HotkeyQuit:
                    Application.Current.Shutdown();
                    handled = true;
                    break;
            }
        }

        return nint.Zero;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        // Actualizar el texto con el ultimo FPS medido.
        double? fps = _fps.CurrentFps;
        if (fps is { } v)
        {
            FpsText.Text = Math.Round(v).ToString("0", CultureInfo.InvariantCulture);
            FpsText.Foreground = v switch
            {
                >= 60 => BrushGood,
                >= 30 => BrushWarn,
                _     => BrushBad,
            };
            FpsText.Visibility = Visibility.Visible;
        }
        else
        {
            FpsText.Visibility = Visibility.Collapsed;
        }

        // Reafirmar que seguimos por encima de todo.
        Topmost = true;

        // Seguir al proceso en primer plano.
        uint pid = ForegroundService.GetForegroundProcessId();
        if (pid == 0 || pid == _ownPid || pid == _lastRequestedPid || _switching)
            return;

        _switching = true;
        _lastRequestedPid = pid;
        try
        {
            await _fps.TrackAsync(pid);
        }
        finally
        {
            _switching = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();

        nint hWnd = new WindowInteropHelper(this).Handle;
        if (hWnd != 0)
        {
            NativeMethods.UnregisterHotKey(hWnd, HotkeyToggle);
            NativeMethods.UnregisterHotKey(hWnd, HotkeyQuit);
        }

        _fps.Dispose();
        base.OnClosed(e);
    }
}
