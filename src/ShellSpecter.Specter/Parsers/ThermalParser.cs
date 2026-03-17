namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Reads CPU thermal zone temperatures and detects frequency scaling.
/// </summary>
public sealed class ThermalParser
{
    public void ApplyTemperatures(Shared.CpuCoreSnapshot[] cores)
    {
        if (!OperatingSystem.IsLinux() || cores.Length == 0) return;

        try
        {
            // Try to read thermal zones — typically zone0 is the CPU package temp
            for (int i = 0; i < 20; i++)
            {
                var typePath = $"/sys/class/thermal/thermal_zone{i}/type";
                var tempPath = $"/sys/class/thermal/thermal_zone{i}/temp";

                if (!File.Exists(typePath) || !File.Exists(tempPath)) continue;

                var type = File.ReadAllText(typePath).Trim();

                // x86_pkg_temp or coretemp types indicate CPU
                if (type.Contains("x86_pkg_temp", StringComparison.OrdinalIgnoreCase) ||
                    type.Contains("coretemp", StringComparison.OrdinalIgnoreCase) ||
                    type.Contains("cpu", StringComparison.OrdinalIgnoreCase))
                {
                    var tempText = File.ReadAllText(tempPath).Trim();
                    if (int.TryParse(tempText, out int milliDegrees))
                    {
                        double tempC = milliDegrees / 1000.0;
                        // Apply same package temp to all cores (best effort)
                        foreach (var core in cores)
                            core.TemperatureC = tempC;
                    }
                    break;
                }
            }
        }
        catch
        {
            // Thermal data is non-critical; continue silently
        }
    }
}
