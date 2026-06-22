using System.Drawing;
using System.Windows;
using Gideon.Services;
using WinForms = System.Windows.Forms;

namespace Gideon;

public partial class App : Application
{
    private WinForms.NotifyIcon _trayIcon = null!;
    private OverlayWindow _overlay = null!;
    private TrayPopup? _popup;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!FpsService.IsRunAsAdmin)
        {
            MessageBox.Show(
                "Gideon necesita ejecutarse como administrador para leer los FPS (ETW).\n\n" +
                "Cierra y vuelve a abrirlo con 'Ejecutar como administrador'.",
                "Gideon", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        var settings = SettingsService.Load();
        _overlay = new OverlayWindow(settings.Corner);
        if (settings.OverlayVisible)
            _overlay.Show();
        else
            _overlay.ShowInvisible();

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "Gideon - FPS Overlay",
            Visible = true,
        };
        _trayIcon.MouseClick += OnTrayMouseClick;

        // Click derecho: menú mínimo con solo "Salir"
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Salir", null, (_, _) => Dispatcher.Invoke(Shutdown));
        _trayIcon.ContextMenuStrip = menu;

        Exit += (_, _) =>
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        };
    }

    private void OnTrayMouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button != WinForms.MouseButtons.Left) return;
        Dispatcher.Invoke(TogglePopup);
    }

    private void TogglePopup()
    {
        if (_popup is null || !_popup.IsLoaded)
            _popup = new TrayPopup(_overlay);

        if (_popup.IsVisible)
        {
            _popup.Hide();
            return;
        }

        // Posicionar justo encima del tray (esquina inferior derecha del área de trabajo)
        var area = SystemParameters.WorkArea;
        _popup.Left = area.Right  - _popup.Width  - 14;
        _popup.Top  = area.Bottom - _popup.Height - 14;

        _popup.Show();
        _popup.Activate();
    }

    // Paleta del icono oficial
    internal static readonly Color ClrGreen  = Color.FromArgb(109, 200,   0); // #6DC800
    internal static readonly Color ClrYellow = Color.FromArgb(245, 200,   0); // #F5C800
    internal static readonly Color ClrWhite  = Color.White;

    private static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode    = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        // Fondo blanco circular (igual que el icono adjunto)
        using (var bg = new SolidBrush(ClrWhite))
            g.FillEllipse(bg, 1, 1, 30, 30);

        // Aro verde lima grueso
        using (var pen = new System.Drawing.Pen(ClrGreen, 3f))
            g.DrawEllipse(pen, 2, 2, 28, 28);

        // "G" verde lima
        using var font = new Font("Arial", 14f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using (var brush = new SolidBrush(ClrGreen))
            g.DrawString("G", font, brush, 7f, 7f);

        // Barra amarilla inferior (guiño a las barras del icono)
        using (var yBrush = new SolidBrush(ClrYellow))
            g.FillRectangle(yBrush, 8f, 26f, 16f, 3f);

        return Icon.FromHandle(bmp.GetHicon());
    }
}
