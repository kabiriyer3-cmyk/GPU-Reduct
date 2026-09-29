using System.ComponentModel;
using System.Windows;
using GpuReduct.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace GpuReduct.Services;

/// <summary>
/// Tray icon: live VRAM % badge (or the app logo), left-click opens the window,
/// right-click menu with Open / Clean now / icon mode / Exit.
/// </summary>
public sealed class TrayService : IDisposable
{
    private static readonly Uri LogoUri = new("pack://application:,,,/Assets/GpuReduct.ico");

    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _showUsageItem;
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly Drawing.Icon _logo;
    private readonly int _iconSize;

    private Drawing.Icon? _usageIcon;
    private int _lastPercent = -1;
    private string _lastLevel = "";

    public TrayService(MainWindow window, MainViewModel viewModel)
    {
        _window = window;
        _viewModel = viewModel;
        _iconSize = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
        _logo = LoadLogo(_iconSize);

        _showUsageItem = new Forms.ToolStripMenuItem("Show VRAM % in tray icon") { CheckOnClick = true, Checked = true };
        _showUsageItem.CheckedChanged += (_, _) => UpdateIcon(force: true);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowWindow());
        menu.Items.Add("Clean now", null, (_, _) =>
        {
            if (_viewModel.CleanCommand.CanExecute(null)) _viewModel.CleanCommand.Execute(null);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_showUsageItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = _logo, // until the first sample arrives
            Text = "GPU Reduct",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) ShowWindow();
        };
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        // TrayText is raised once per sample, after all numbers are updated.
        if (e.PropertyName != nameof(MainViewModel.TrayText)) return;

        string text = _viewModel.TrayText;
        _icon.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon text limit
        UpdateIcon(force: false);
    }

    private void UpdateIcon(bool force)
    {
        if (!_showUsageItem.Checked)
        {
            _icon.Icon = _logo;
            DisposeUsageIcon();
            _lastPercent = -1;
            return;
        }

        int percent = (int)Math.Round(_viewModel.UsagePercent);
        string level = _viewModel.UsageLevel;
        if (!force && percent == _lastPercent && level == _lastLevel) return; // nothing visible changed

        _lastPercent = percent;
        _lastLevel = level;

        var previous = _usageIcon;
        _usageIcon = TrayIconRenderer.Render(percent, level, _iconSize);
        _icon.Icon = _usageIcon;
        previous?.Dispose();
    }

    private void DisposeUsageIcon()
    {
        _usageIcon?.Dispose();
        _usageIcon = null;
    }

    private static Drawing.Icon LoadLogo(int size)
    {
        try
        {
            var resource = Application.GetResourceStream(LogoUri);
            if (resource != null)
            {
                using var stream = resource.Stream;
                return new Drawing.Icon(stream, size, size);
            }
        }
        catch
        {
            // fall through to the default icon
        }
        return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();
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
        DisposeUsageIcon();
        _logo.Dispose();
    }
}
