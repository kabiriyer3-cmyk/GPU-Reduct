using System.Windows;
using GpuReduct.Core;
using GpuReduct.Services;
using GpuReduct.ViewModels;

namespace GpuReduct;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private TrayService? _tray;
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\GpuReduct.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        if (!GpuMonitor.IsSupported)
        {
            MessageBox.Show(
                "GPU performance counters are not available.\nWindows 10 1709+ with a WDDM 2.x driver is required.",
                "GPU Reduct", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var window = new MainWindow();
        _viewModel = new MainViewModel
        {
            // Keeps the VM free of UI types; swap for a custom styled dialog later.
            Confirm = message => MessageBox.Show(window, message, "GPU Reduct",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes
        };
        window.DataContext = _viewModel;

        _tray = new TrayService(window, _viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _viewModel?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
