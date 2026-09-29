using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using GpuReduct.Core;

namespace GpuReduct.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const int HistoryLength = 60;
    private const int TopProcessCount = 6;
    private static readonly TimeSpan AutoCleanCooldown = TimeSpan.FromMinutes(1);

    private readonly GpuMonitor _monitor = new();
    private readonly DispatcherTimer _timer;
    private readonly double[] _history = new double[HistoryLength];
    private GpuSnapshot? _last;
    private bool _sampling;
    private DateTime _lastAutoClean = DateTime.MinValue;

    public MainViewModel()
    {
        foreach (var adapter in DxgiAdapters.Enumerate()) Adapters.Add(adapter);

        // Default to the GPU with the most VRAM (the discrete card on hybrid laptops).
        _selectedAdapter = Adapters.OrderByDescending(a => a.DedicatedBytes).FirstOrDefault();

        CleanCommand = new AsyncRelayCommand(_ => CleanAsync(), _ => !IsBusy && _last != null);
        ResetDriverCommand = new AsyncRelayCommand(_ => ResetDriverAsync(), _ => !IsBusy);
        KillProcessCommand = new RelayCommand(KillProcess);
        NextAdapterCommand = new RelayCommand(_ => NextAdapter());

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    // ───── Collections & commands ─────
    public ObservableCollection<GpuAdapter> Adapters { get; } = new();
    public ObservableCollection<ProcessItem> Processes { get; } = new();

    public ICommand CleanCommand { get; }
    public ICommand ResetDriverCommand { get; }
    public ICommand KillProcessCommand { get; }
    public ICommand NextAdapterCommand { get; }

    /// <summary>Injected by the view so the VM stays UI-agnostic.</summary>
    public Func<string, bool> Confirm { get; set; } = _ => true;

    // ───── Adapter ─────
    private GpuAdapter? _selectedAdapter;
    public GpuAdapter? SelectedAdapter
    {
        get => _selectedAdapter;
        set
        {
            if (!SetProperty(ref _selectedAdapter, value)) return;
            Array.Clear(_history);
            Processes.Clear();
            OnPropertyChanged(nameof(AdapterName));
            _ = RefreshAsync();
        }
    }

    public string AdapterName => SelectedAdapter?.Name ?? "No GPU detected";
    public bool HasMultipleAdapters => Adapters.Count > 1;

    // ───── Display state ─────
    private string _usedText = "—";
    public string UsedText { get => _usedText; private set => SetProperty(ref _usedText, value); }

    private string _totalText = "—";
    public string TotalText { get => _totalText; private set => SetProperty(ref _totalText, value); }

    private string _percentText = "—";
    public string PercentText { get => _percentText; private set => SetProperty(ref _percentText, value); }

    private double _usagePercent;
    public double UsagePercent { get => _usagePercent; private set => SetProperty(ref _usagePercent, value); }

    private string _usageLevel = "Normal";
    public string UsageLevel { get => _usageLevel; private set => SetProperty(ref _usageLevel, value); }

    private string _sharedText = "—";
    public string SharedText { get => _sharedText; private set => SetProperty(ref _sharedText, value); }

    private double _loadPercent;
    public double LoadPercent { get => _loadPercent; private set => SetProperty(ref _loadPercent, value); }

    private string _loadText = "—";
    public string LoadText { get => _loadText; private set => SetProperty(ref _loadText, value); }

    private string _processCountText = "—";
    public string ProcessCountText { get => _processCountText; private set => SetProperty(ref _processCountText, value); }

    private double[] _historySnapshot = new double[HistoryLength];
    public double[] History { get => _historySnapshot; private set => SetProperty(ref _historySnapshot, value); }

    private string _statusText = "Ready · sampling every second";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public string TrayText => $"GPU Reduct · {UsedText} / {TotalText} ({PercentText})";

    // ───── Auto clean ─────
    private bool _autoCleanEnabled;
    public bool AutoCleanEnabled { get => _autoCleanEnabled; set => SetProperty(ref _autoCleanEnabled, value); }

    private int _autoCleanThreshold = 85;
    public int AutoCleanThreshold { get => _autoCleanThreshold; set => SetProperty(ref _autoCleanThreshold, value); }

    // ───── Sampling ─────
    private async Task RefreshAsync()
    {
        if (_sampling || SelectedAdapter is not { } adapter) return;
        _sampling = true;
        try
        {
            var snapshot = await Task.Run(() => _monitor.Sample(adapter.LuidKey));
            if (!Equals(adapter, SelectedAdapter)) return; // user switched GPU mid-sample
            Apply(adapter, snapshot);
            await MaybeAutoCleanAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Sampling failed: {ex.Message}";
        }
        finally
        {
            _sampling = false;
        }
    }

    private void Apply(GpuAdapter adapter, GpuSnapshot s)
    {
        _last = s;

        double pct = adapter.DedicatedBytes == 0 ? 0 : Math.Min(100, s.DedicatedUsed * 100.0 / adapter.DedicatedBytes);
        UsagePercent = pct;
        PercentText = $"{pct:0}%";
        UsageLevel = pct >= 90 ? "Critical" : pct >= 75 ? "Warn" : "Normal";
        UsedText = ByteSize.Format(s.DedicatedUsed);
        TotalText = ByteSize.Format(adapter.DedicatedBytes);
        SharedText = ByteSize.Format(s.SharedUsed);
        LoadPercent = s.LoadPercent;
        LoadText = $"{s.LoadPercent:0}%";
        ProcessCountText = s.Processes.Count.ToString();

        Array.Copy(_history, 1, _history, 0, HistoryLength - 1);
        _history[^1] = pct;
        History = (double[])_history.Clone(); // new instance -> Sparkline re-renders

        SyncProcesses(s.Processes.Take(TopProcessCount).ToList(), adapter.DedicatedBytes);
        OnPropertyChanged(nameof(TrayText));
    }

    /// <summary>Updates rows in place (no flicker, hover state survives).</summary>
    private void SyncProcesses(IReadOnlyList<GpuProcessUsage> top, ulong adapterTotal)
    {
        for (int i = 0; i < top.Count; i++)
        {
            var usage = top[i];
            int existing = -1;
            for (int j = i; j < Processes.Count; j++)
                if (Processes[j].Pid == usage.Pid) { existing = j; break; }

            ProcessItem item;
            if (existing < 0)
            {
                item = new ProcessItem(usage.Pid, usage.Name);
                Processes.Insert(i, item);
            }
            else
            {
                item = Processes[existing];
                if (existing != i) Processes.Move(existing, i);
            }
            item.Update(usage, adapterTotal);
        }

        while (Processes.Count > top.Count) Processes.RemoveAt(Processes.Count - 1);
    }

    // ───── Actions ─────
    private async Task CleanAsync()
    {
        if (SelectedAdapter is not { } adapter || _last is not { } before) return;

        IsBusy = true;
        StatusText = "Cleaning…";
        try
        {
            var pids = before.Processes.Select(p => p.Pid).ToList();
            int trimmed = await Task.Run(() => GpuCleaner.TrimWorkingSets(pids));

            await Task.Delay(1500); // let the video memory manager settle before re-measuring
            var after = await Task.Run(() => _monitor.Sample(adapter.LuidKey));
            Apply(adapter, after);

            StatusText = Summary($"Trimmed {trimmed} processes",
                before.DedicatedUsed + before.SharedUsed, after.DedicatedUsed + after.SharedUsed);
        }
        catch (Exception ex)
        {
            StatusText = $"Clean failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ResetDriverAsync()
    {
        if (SelectedAdapter is not { } adapter) return;
        if (!Confirm("Restart the graphics driver?\n\nThe screen will go black for a moment. " +
                     "Games and 3D apps may crash or lose their rendering device.")) return;

        IsBusy = true;
        StatusText = "Restarting graphics driver…";
        try
        {
            ulong before = _last?.DedicatedUsed ?? 0;
            GpuCleaner.ResetGraphicsDriver();

            await Task.Delay(TimeSpan.FromSeconds(3)); // driver needs a moment to come back
            var after = await Task.Run(() => _monitor.Sample(adapter.LuidKey));
            Apply(adapter, after);

            StatusText = Summary("Driver restarted", before, after.DedicatedUsed);
        }
        catch (Exception ex)
        {
            StatusText = $"Driver reset failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void KillProcess(object? parameter)
    {
        if (parameter is not int pid) return;

        string name = Processes.FirstOrDefault(p => p.Pid == pid)?.Name ?? $"PID {pid}";
        if (!Confirm($"End \"{name}\" (PID {pid})?\n\nUnsaved work in that app will be lost.")) return;

        StatusText = GpuCleaner.KillProcess(pid)
            ? $"Ended {name} · {DateTime.Now:t}"
            : $"Couldn't end {name} (access denied?)";
    }

    private void NextAdapter()
    {
        if (Adapters.Count < 2 || SelectedAdapter is null) return;
        int index = Adapters.IndexOf(SelectedAdapter);
        SelectedAdapter = Adapters[(index + 1) % Adapters.Count];
    }

    private async Task MaybeAutoCleanAsync()
    {
        if (!AutoCleanEnabled || IsBusy || UsagePercent < AutoCleanThreshold) return;
        if (DateTime.Now - _lastAutoClean < AutoCleanCooldown) return;

        _lastAutoClean = DateTime.Now;
        await CleanAsync();
    }

    private static string Summary(string action, ulong before, ulong after)
    {
        string result = before > after ? $"freed {ByteSize.Format(before - after)}" : "nothing to reclaim";
        return $"{action} · {result} · {DateTime.Now:t}";
    }

    public void Dispose() => _timer.Stop();
}
