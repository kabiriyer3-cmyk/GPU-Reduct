using System.Runtime.InteropServices;

namespace GpuReduct.Core;

internal static class Native
{
    public const uint PROCESS_SET_QUOTA = 0x0100;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("psapi.dll", SetLastError = true)]
    public static extern bool EmptyWorkingSet(IntPtr processHandle);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extraInfo);
}
