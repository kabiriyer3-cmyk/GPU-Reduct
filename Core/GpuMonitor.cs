using System.Diagnostics;

namespace GpuReduct.Core;

public sealed record GpuProcessUsage(int Pid, string Name, ulong DedicatedBytes, ulong SharedBytes);

public sealed record GpuSnapshot(ulong DedicatedUsed, ulong SharedUsed, double LoadPercent, IReadOnlyList<GpuProcessUsage> Processes);

/// <summary>
/// Reads the Windows "GPU *" performance counters (same data as Task Manager).
/// Instance names look like: pid_1234_luid_0x00000000_0x0000D1B2_phys_0[_eng_3_engtype_3D]
/// </summary>
public sealed class GpuMonitor
{
    private const string AdapterMemoryCategory = "GPU Adapter Memory";
    private const string ProcessMemoryCategory = "GPU Process Memory";
    private const string EngineCategory = "GPU Engine";

    private readonly object _gate = new();
    private readonly Dictionary<string, CounterSample> _previousEngineSamples = new();
    private readonly Dictionary<int, string> _processNames = new();
    private readonly bool _hasEngineCounters = SafeExists(EngineCategory);

    public static bool IsSupported => SafeExists(AdapterMemoryCategory) && SafeExists(ProcessMemoryCategory);

    public GpuSnapshot Sample(string luidKey)
    {
        lock (_gate)
        {
            var adapter = new PerformanceCounterCategory(AdapterMemoryCategory).ReadCategory();
            ulong dedicated = SumFor(adapter["Dedicated Usage"], luidKey);
            ulong shared = SumFor(adapter["Shared Usage"], luidKey);

            var perProcess = new Dictionary<int, (ulong Dedicated, ulong Shared)>();
            var processData = new PerformanceCounterCategory(ProcessMemoryCategory).ReadCategory();

            foreach (var (pid, value) in ByPid(processData["Dedicated Usage"], luidKey))
            {
                perProcess.TryGetValue(pid, out var cur);
                perProcess[pid] = (cur.Dedicated + value, cur.Shared);
            }
            foreach (var (pid, value) in ByPid(processData["Shared Usage"], luidKey))
            {
                perProcess.TryGetValue(pid, out var cur);
                perProcess[pid] = (cur.Dedicated, cur.Shared + value);
            }

            if (_processNames.Count > 1024) _processNames.Clear(); // PIDs get reused

            var processes = perProcess
                .Where(p => p.Value.Dedicated + p.Value.Shared > 0)
                .Select(p => new GpuProcessUsage(p.Key, ResolveName(p.Key), p.Value.Dedicated, p.Value.Shared))
                .OrderByDescending(p => p.DedicatedBytes)
                .ThenByDescending(p => p.SharedBytes)
                .ToList();

            return new GpuSnapshot(dedicated, shared, ReadLoad(luidKey), processes);
        }
    }

    /// <summary>Task Manager style: sum utilization per engine across processes, report the busiest engine.</summary>
    private double ReadLoad(string luidKey)
    {
        if (!_hasEngineCounters) return 0;

        var column = new PerformanceCounterCategory(EngineCategory).ReadCategory()["Utilization Percentage"];
        if (column is null) return 0;

        var perEngine = new Dictionary<string, double>();
        var seen = new HashSet<string>();

        foreach (InstanceData d in column.Values)
        {
            if (!d.InstanceName.Contains(luidKey, StringComparison.OrdinalIgnoreCase)) continue;
            seen.Add(d.InstanceName);

            // Utilization is a rate counter: it needs two samples to produce a value.
            if (_previousEngineSamples.TryGetValue(d.InstanceName, out var previous))
            {
                string engine = EngineKey(d.InstanceName);
                perEngine[engine] = perEngine.GetValueOrDefault(engine) + CounterSample.Calculate(previous, d.Sample);
            }
            _previousEngineSamples[d.InstanceName] = d.Sample;
        }

        foreach (var stale in _previousEngineSamples.Keys.Where(k => !seen.Contains(k)).ToList())
            _previousEngineSamples.Remove(stale);

        return perEngine.Count == 0 ? 0 : Math.Clamp(perEngine.Values.Max(), 0, 100);
    }

    private static ulong SumFor(InstanceDataCollection? column, string luidKey)
    {
        if (column is null) return 0;
        ulong total = 0;
        foreach (InstanceData d in column.Values)
            if (d.InstanceName.Contains(luidKey, StringComparison.OrdinalIgnoreCase))
                total += (ulong)Math.Max(0L, d.RawValue);
        return total;
    }

    private static IEnumerable<(int Pid, ulong Value)> ByPid(InstanceDataCollection? column, string luidKey)
    {
        if (column is null) yield break;
        foreach (InstanceData d in column.Values)
        {
            if (!d.InstanceName.Contains(luidKey, StringComparison.OrdinalIgnoreCase)) continue;
            int pid = ParsePid(d.InstanceName);
            if (pid > 0) yield return (pid, (ulong)Math.Max(0L, d.RawValue));
        }
    }

    private static int ParsePid(string instance)
    {
        var parts = instance.Split('_');
        return parts.Length > 1 && parts[0] == "pid" && int.TryParse(parts[1], out int pid) ? pid : -1;
    }

    private static string EngineKey(string instance)
    {
        int start = instance.IndexOf("_eng_", StringComparison.Ordinal);
        if (start < 0) return instance;
        int end = instance.IndexOf("_engtype", start, StringComparison.Ordinal);
        return end < 0 ? instance[(start + 1)..] : instance[(start + 1)..end];
    }

    private string ResolveName(int pid)
    {
        if (_processNames.TryGetValue(pid, out var name)) return name;
        try
        {
            using var p = Process.GetProcessById(pid);
            name = p.ProcessName;
        }
        catch
        {
            name = $"PID {pid}";
        }
        _processNames[pid] = name;
        return name;
    }

    private static bool SafeExists(string category)
    {
        try { return PerformanceCounterCategory.Exists(category); }
        catch { return false; }
    }
}
