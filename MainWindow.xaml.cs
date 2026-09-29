using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GpuReduct;

public partial class MainWindow : Window
{
    /// <summary>Set by the tray "Exit" item; otherwise closing just hides to tray.</summary>
    public bool AllowClose { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyWindows11Chrome();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    // Close the GPU dropdown after a pick (also when re-picking the current GPU).
    private void AdapterList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        AdapterToggle.IsChecked = false;

    private void AdapterList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape) AdapterToggle.IsChecked = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    // Win11 only; the calls fail silently on Windows 10.
    private void ApplyWindows11Chrome()
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        int border = 0x0023201F; // COLORREF (0x00BBGGRR) for #1F2023
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
