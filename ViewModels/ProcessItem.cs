using GpuReduct.Core;

namespace GpuReduct.ViewModels;

public sealed class ProcessItem : ObservableObject
{
    private string _dedicatedText = "";
    private double _sharePercent;

    public ProcessItem(int pid, string name)
    {
        Pid = pid;
        Name = name;
    }

    public int Pid { get; }
    public string Name { get; }

    public string DedicatedText
    {
        get => _dedicatedText;
        private set => SetProperty(ref _dedicatedText, value);
    }

    /// <summary>Share of the adapter's total VRAM (0–100); drives the mini bar.</summary>
    public double SharePercent
    {
        get => _sharePercent;
        private set => SetProperty(ref _sharePercent, value);
    }

    public void Update(GpuProcessUsage usage, ulong adapterTotal)
    {
        DedicatedText = ByteSize.Format(usage.DedicatedBytes);
        SharePercent = adapterTotal == 0 ? 0 : Math.Min(100, usage.DedicatedBytes * 100.0 / adapterTotal);
    }
}
