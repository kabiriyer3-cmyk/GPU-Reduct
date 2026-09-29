using System.Runtime.InteropServices;

namespace GpuReduct.Core;

/// <summary>Physical GPU as reported by DXGI.</summary>
public sealed record GpuAdapter(string Name, uint VendorId, ulong DedicatedBytes, ulong SharedBytes, string LuidKey)
{
    public override string ToString() => Name;
}

internal static class DxgiAdapters
{
    private const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;
    private const uint MicrosoftVendorId = 0x1414; // Basic Render Driver

    public static IReadOnlyList<GpuAdapter> Enumerate()
    {
        var result = new List<GpuAdapter>();
        Guid iid = typeof(IDXGIFactory1).GUID;
        if (CreateDXGIFactory1(ref iid, out var obj) < 0 || obj is not IDXGIFactory1 factory)
            return result;

        try
        {
            for (uint i = 0; factory.EnumAdapters1(i, out var adapter) >= 0; i++)
            {
                try
                {
                    if (adapter.GetDesc1(out var d) < 0) continue;
                    if ((d.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0 || d.VendorId == MicrosoftVendorId) continue;

                    // Same "luid_0x..._0x..." format the GPU perf counter instances use.
                    string luid = $"luid_0x{d.AdapterLuidHighPart:X8}_0x{d.AdapterLuidLowPart:X8}";
                    result.Add(new GpuAdapter(d.Description.Trim(), d.VendorId,
                        (ulong)d.DedicatedVideoMemory, (ulong)d.SharedSystemMemory, luid));
                }
                finally
                {
                    Marshal.ReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(factory);
        }

        return result;
    }

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object factory);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DXGI_ADAPTER_DESC1
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    public uint VendorId;
    public uint DeviceId;
    public uint SubSysId;
    public uint Revision;
    public nuint DedicatedVideoMemory;
    public nuint DedicatedSystemMemory;
    public nuint SharedSystemMemory;
    public uint AdapterLuidLowPart;
    public int AdapterLuidHighPart;
    public uint Flags;
}

// Vtable order matters: unused base methods are declared as placeholders.
[ComImport, Guid("770aae78-f26f-4dba-a829-253c83d1b387"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIFactory1
{
    void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();          // IDXGIObject
    void EnumAdapters(); void MakeWindowAssociation(); void GetWindowAssociation(); void CreateSwapChain();  // IDXGIFactory
    void CreateSoftwareAdapter();
    [PreserveSig] int EnumAdapters1(uint index, out IDXGIAdapter1 adapter);                                // IDXGIFactory1
    [PreserveSig] bool IsCurrent();
}

[ComImport, Guid("29038f61-3839-4626-91fd-086879011a05"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDXGIAdapter1
{
    void SetPrivateData(); void SetPrivateDataInterface(); void GetPrivateData(); void GetParent();          // IDXGIObject
    void EnumOutputs(); void GetDesc(); void CheckInterfaceSupport();                                        // IDXGIAdapter
    [PreserveSig] int GetDesc1(out DXGI_ADAPTER_DESC1 desc);                                                 // IDXGIAdapter1
}
