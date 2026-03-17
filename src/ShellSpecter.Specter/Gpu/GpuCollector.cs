using System.Text;

namespace ShellSpecter.Specter.Gpu;

/// <summary>
/// High-level GPU data collector wrapping NVML. Handles the case where NVML is not available.
/// </summary>
public sealed class GpuCollector : IDisposable
{
    private bool _initialized;
    private readonly ILogger<GpuCollector> _logger;

    public GpuCollector(ILogger<GpuCollector> logger)
    {
        _logger = logger;
        TryInitialize();
    }

    private void TryInitialize()
    {
        if (!OperatingSystem.IsLinux()) return;

        try
        {
            int result = NvmlInterop.NvmlInit();
            if (result == NvmlInterop.NVML_SUCCESS)
            {
                _initialized = true;
                _logger.LogInformation("NVML initialized successfully");
            }
            else
            {
                _logger.LogWarning("NVML init returned code {Code}", result);
            }
        }
        catch (DllNotFoundException)
        {
            _logger.LogInformation("NVML library not found — GPU monitoring disabled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize NVML");
        }
    }

    public Shared.GpuSnapshot[] Collect()
    {
        if (!_initialized) return [];

        try
        {
            int countResult = NvmlInterop.NvmlDeviceGetCount(out uint count);
            if (countResult != NvmlInterop.NVML_SUCCESS) return [];

            var snapshots = new Shared.GpuSnapshot[count];

            for (uint i = 0; i < count; i++)
            {
                var gpu = new Shared.GpuSnapshot { Index = (int)i };

                if (NvmlInterop.NvmlDeviceGetHandleByIndex(i, out IntPtr device) != NvmlInterop.NVML_SUCCESS)
                    continue;

                // Name
                var nameBuffer = new StringBuilder(256);
                if (NvmlInterop.NvmlDeviceGetName(device, nameBuffer, 256) == NvmlInterop.NVML_SUCCESS)
                    gpu.Name = nameBuffer.ToString();

                // Utilization
                if (NvmlInterop.NvmlDeviceGetUtilizationRates(device, out var util) == NvmlInterop.NVML_SUCCESS)
                    gpu.CoreLoadPercent = util.Gpu;

                // Memory
                if (NvmlInterop.NvmlDeviceGetMemoryInfo(device, out var mem) == NvmlInterop.NVML_SUCCESS)
                {
                    gpu.VramTotalMb = (long)(mem.Total / (1024 * 1024));
                    gpu.VramUsedMb = (long)(mem.Used / (1024 * 1024));
                }

                // Power
                if (NvmlInterop.NvmlDeviceGetPowerUsage(device, out uint powerMw) == NvmlInterop.NVML_SUCCESS)
                    gpu.PowerWatts = powerMw / 1000.0;

                // Temperature
                if (NvmlInterop.NvmlDeviceGetTemperature(device, NvmlInterop.NVML_TEMPERATURE_GPU, out uint temp) == NvmlInterop.NVML_SUCCESS)
                    gpu.TemperatureC = temp;

                snapshots[i] = gpu;
            }

            return snapshots;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting GPU data");
            return [];
        }
    }

    public void Dispose()
    {
        if (_initialized)
        {
            try { NvmlInterop.NvmlShutdown(); } catch { }
            _initialized = false;
        }
    }
}
