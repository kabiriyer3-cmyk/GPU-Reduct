using System.ComponentModel;
using System.Windows;
using GpuReduct.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace GpuReduct.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;

    public TrayService(MainWindow window, MainViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("Clean now", null, (_, _) =>
        {
            if (_viewModel.CleanCommand.CanExecute(null)) _viewModel.CleanCommand.Execute(null);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "GPU Reduct",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowWindow();
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.TrayText)) return;
        string text = _viewModel.TrayText;
        _icon.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon text limit
    }

    private static Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } path && Drawing.Icon.ExtractAssociatedIcon(path) is { } icon)
                return icon;
        }
        catch
        {
            // fall through to the default icon
        }
        return Drawing.SystemIcons.Application;
    }

    private void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void Exit()
    {
        _window.AllowClose = true;
        _window.Close();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
