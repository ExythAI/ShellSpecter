using System.Runtime.InteropServices;

namespace ShellSpecter.Specter.Gpu;

/// <summary>
/// P/Invoke wrapper for NVIDIA Management Library (libnvidia-ml.so / NVML).
/// </summary>
public static class NvmlInterop
{
    private const string NvmlLibrary = "nvidia-ml";

    public const int NVML_SUCCESS = 0;
    public const int NVML_TEMPERATURE_GPU = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct NvmlUtilization
    {
        public uint Gpu;
        public uint Memory;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NvmlMemory
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }

    [DllImport(NvmlLibrary, EntryPoint = "nvmlInit_v2")]
    public static extern int NvmlInit();

    [DllImport(NvmlLibrary, EntryPoint = "nvmlShutdown")]
    public static extern int NvmlShutdown();

    [DllImport(NvmlLibrary, EntryPoint = "nvmlDeviceGetCount_v2")]
    public static extern int NvmlDeviceGetCount(out uint deviceCount);

    [DllImport(NvmlLibrary, EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    public static extern int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

    [DllImport(NvmlLibrary, EntryPoint = "nvmlDeviceGetName")]
    public static extern int NvmlDeviceGetName(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder name, uint length);

    [DllImport(NvmlLibrary, EntryPoint = "nvmlDeviceGetUtilizationRates")]
    public static extern int NvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);

    [DllImport(NvmlLibrary, EntryPoint = "nvmlDeviceGetMemoryInfo")]
    public static extern int NvmlDeviceGetMemoryInfo(IntPtr device, out NvmlMemory memory);

    [DllImport(NvmlLibrary, EntryPoint = "nvmlDeviceGetPowerUsage")]
    public static extern int NvmlDeviceGetPowerUsage(IntPtr device, out uint powerMilliwatts);

    [DllImport(NvmlLibrary, EntryPoint = "nvmlDeviceGetTemperature")]
    public static extern int NvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temperature);
}
