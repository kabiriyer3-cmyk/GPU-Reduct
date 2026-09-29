namespace GpuReduct.Core;

public static class ByteSize
{
    private const double MB = 1024d * 1024d;
    private const double GB = MB * 1024d;

    public static string Format(ulong bytes) =>
        bytes >= GB ? $"{bytes / GB:0.00} GB" : $"{bytes / MB:0} MB";
}
