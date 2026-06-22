using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Gideon.Services;
using System.Windows.Controls;

namespace Gideon;

public partial class TrayPopup : Window
{
    private static readonly SolidColorBrush ActiveBg =
        new(Color.FromArgb(60, 109, 200, 0));      // verde lima con alpha
    private static readonly SolidColorBrush InactiveBg =
        new(Color.FromRgb(31, 41, 55));
    private static readonly SolidColorBrush GreenFg =
        new(Color.FromRgb(109, 200, 0));            // #6DC800
    private static readonly SolidColorBrush RedFg =
        new(Color.FromRgb(239, 68, 68));

    private readonly OverlayWindow _overlay;

    public TrayPopup(OverlayWindow overlay)
    {
        _overlay = overlay;
        InitializeComponent();
    }

    // Refresca estado visual cada vez que el popup se hace visible.
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        Refresh();
    }

    private void Refresh()
    {
        bool running = _overlay.IsVisible;
        ToggleBtn.Content = running ? "Detener overlay" : "Iniciar overlay";
        ToggleBtn.Foreground = running ? RedFg : GreenFg;
        StatusDot.Fill = running ? GreenFg : RedFg;
        HighlightCorner(_overlay.CurrentCorner);
        ChkStartup.IsChecked = StartupService.IsEnabled();
    }

    private void HighlightCorner(Corner corner)
    {
        BtnTL.Background = corner == Corner.TopLeft     ? ActiveBg : InactiveBg;
        BtnTR.Background = corner == Corner.TopRight    ? ActiveBg : InactiveBg;
        BtnBL.Background = corner == Corner.BottomLeft  ? ActiveBg : InactiveBg;
        BtnBR.Background = corner == Corner.BottomRight ? ActiveBg : InactiveBg;
    }

    private void OnToggle(object sender, RoutedEventArgs e)
    {
        if (_overlay.IsVisible)
            _overlay.Hide();
        else
            _overlay.Show();

        SettingsService.Save(new AppSettings
        {
            Corner = _overlay.CurrentCorner,
            OverlayVisible = _overlay.IsVisible,
        });
        Refresh();
    }

    private void OnCorner(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn &&
            Enum.TryParse<Corner>(btn.Tag?.ToString(), out var corner))
        {
            _overlay.SetCorner(corner);
            SettingsService.Save(new AppSettings { Corner = corner });
            HighlightCorner(corner);
        }
    }

    private void OnStartupToggle(object sender, RoutedEventArgs e)
    {
        if (ChkStartup.IsChecked == true)
            StartupService.Enable();
        else
            StartupService.Disable();
    }

    private void OnExit(object sender, RoutedEventArgs e) =>
        Application.Current.Shutdown();

    // Ocultar (no cerrar) cuando pierde el foco — se reutiliza en la proxima apertura.
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        Hide();
    }
}
