namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Reads CPU temperatures from /sys/class/hwmon/ and /sys/class/thermal/.
/// Tries multiple sources for maximum hardware compatibility.
/// </summary>
public sealed class ThermalParser
{
    public void ApplyTemperatures(Shared.CpuCoreSnapshot[] cores)
    {
        if (!OperatingSystem.IsLinux() || cores.Length == 0) return;

        try
        {
            // Strategy 1: Try hwmon (most reliable, works on most hardware)
            var temp = ReadHwmonCpuTemp();

            // Strategy 2: Try thermal zones with known CPU type names
            if (temp <= 0) temp = ReadThermalZone(matchCpu: true);

            // Strategy 3: Fall back to first available thermal zone
            if (temp <= 0) temp = ReadThermalZone(matchCpu: false);

            if (temp > 0)
            {
                foreach (var core in cores)
                    core.TemperatureC = temp;
            }
        }
        catch
        {
            // Thermal data is non-critical; continue silently
        }
    }

    /// <summary>
    /// Read CPU temperature from /sys/class/hwmon/ devices.
    /// Looks for coretemp, k10temp, zenpower, acpitz, etc.
    /// </summary>
    private static double ReadHwmonCpuTemp()
    {
        var hwmonDir = "/sys/class/hwmon";
        if (!Directory.Exists(hwmonDir)) return 0;

        string[] cpuDrivers = ["coretemp", "k10temp", "zenpower", "it87", "nct6775", "acpitz", "cpu_thermal"];

        foreach (var device in Directory.GetDirectories(hwmonDir))
        {
            var namePath = Path.Combine(device, "name");
            if (!File.Exists(namePath)) continue;

            var name = File.ReadAllText(namePath).Trim();

            // Check if this is a CPU-related hwmon device
            bool isCpuDevice = cpuDrivers.Any(d =>
                name.Equals(d, StringComparison.OrdinalIgnoreCase));

            if (!isCpuDevice) continue;

            // Read temp1_input (package temp), or temp2_input, etc.
            for (int i = 1; i <= 5; i++)
            {
                var inputPath = Path.Combine(device, $"temp{i}_input");
                if (!File.Exists(inputPath)) continue;

                var text = File.ReadAllText(inputPath).Trim();
                if (int.TryParse(text, out int milliDegrees) && milliDegrees > 0)
                {
                    return milliDegrees / 1000.0;
                }
            }
        }

        // Fallback: try ANY hwmon device with temp1_input
        foreach (var device in Directory.GetDirectories(hwmonDir))
        {
            var inputPath = Path.Combine(device, "temp1_input");
            if (!File.Exists(inputPath)) continue;

            var text = File.ReadAllText(inputPath).Trim();
            if (int.TryParse(text, out int milliDegrees) && milliDegrees > 0)
            {
                return milliDegrees / 1000.0;
            }
        }

        return 0;
    }

    /// <summary>
    /// Read from /sys/class/thermal/thermal_zone*.
    /// If matchCpu is true, only match CPU-related zone types.
    /// If false, return the first available zone.
    /// </summary>
    private static double ReadThermalZone(bool matchCpu)
    {
        string[] cpuTypes = ["x86_pkg_temp", "coretemp", "cpu", "soc", "acpitz"];

        for (int i = 0; i < 20; i++)
        {
            var tempPath = $"/sys/class/thermal/thermal_zone{i}/temp";
            if (!File.Exists(tempPath)) continue;

            if (matchCpu)
            {
                var typePath = $"/sys/class/thermal/thermal_zone{i}/type";
                if (!File.Exists(typePath)) continue;

                var type = File.ReadAllText(typePath).Trim();
                if (!cpuTypes.Any(t => type.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    continue;
            }

            var text = File.ReadAllText(tempPath).Trim();
            if (int.TryParse(text, out int milliDegrees) && milliDegrees > 0)
            {
                return milliDegrees / 1000.0;
            }
        }

        return 0;
    }
}
