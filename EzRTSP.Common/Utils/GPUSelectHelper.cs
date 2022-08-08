using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using EzRTSP.Common.Builder;

namespace EzRTSP.Common.Utils;

// ReSharper disable once InconsistentNaming
public static class GPUSelectHelper
{
    // ReSharper disable once InconsistentNaming
    public static IEnumerable<ManufactureInfo> EnumerateSupportedGPU()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var manufactureInfo in EnumerateWindows()) yield return manufactureInfo;
        }
        //else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        //{
        //    var cmd = "lspci | grep VGA";
        //}
        else
        {
            throw new PlatformNotSupportedException(RuntimeInformation.OSDescription);
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<ManufactureInfo> EnumerateWindows()
    {
        using var searcher = new ManagementObjectSearcher("select * from Win32_VideoController");
        int i = 0;
        foreach (var obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString();
            if (name?.IndexOf("nvidia", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ManufactureInfo(name, i, Manufacture.NVIDIA);
            else if (name?.IndexOf("amd", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ManufactureInfo(name, i, Manufacture.AMD);
            else if (name?.IndexOf("intel", StringComparison.OrdinalIgnoreCase) >= 0)
                yield return new ManufactureInfo(name, i, Manufacture.INTEL);

            i++;
        }
    }
}