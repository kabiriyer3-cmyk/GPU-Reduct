using System.Diagnostics;

namespace GpuReduct.Core;

public static class GpuCleaner
{
    /// <summary>
    /// Empties the working set of each process. Frees RAM and shared GPU memory;
    /// dedicated VRAM residency is still decided by the WDDM video memory manager.
    /// </summary>
    public static int TrimWorkingSets(IEnumerable<int> pids)
    {
        int trimmed = 0;
        foreach (int pid in pids.Distinct())
        {
            IntPtr handle = Native.OpenProcess(Native.PROCESS_SET_QUOTA | Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (handle == IntPtr.Zero) continue; // protected / exited process

            try
            {
                if (Native.EmptyWorkingSet(handle)) trimmed++;
            }
            finally
            {
                Native.CloseHandle(handle);
            }
        }
        return trimmed;
    }

    /// <summary>
    /// Sends Win+Ctrl+Shift+B, the built-in shortcut that restarts the graphics driver.
    /// This is the only reliable way to make Windows drop cached VRAM (DWM, driver caches).
    /// </summary>
    public static void ResetGraphicsDriver()
    {
        const byte VK_LWIN = 0x5B, VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_B = 0x42;
        byte[] keys = { VK_LWIN, VK_CONTROL, VK_SHIFT, VK_B };

        foreach (byte key in keys)
            Native.keybd_event(key, 0, 0, UIntPtr.Zero);

        for (int i = keys.Length - 1; i >= 0; i--)
            Native.keybd_event(keys[i], 0, Native.KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static bool KillProcess(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
