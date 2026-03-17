using System.Diagnostics;
using System.Text.Json;
using ShellSpecter.Shared;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Reads fan speeds (and optionally temperatures) by shelling out to `sensors -j` (lm-sensors).
/// Falls back to direct hwmon sysfs reading if lm-sensors is not installed.
/// </summary>
public sealed class FanParser
{
    private DateTime _lastParsed;
    private FanSnapshot[] _cached = [];

    // Cache for 2 seconds since fan speeds don't change rapidly
    private static readonly TimeSpan CacheInterval = TimeSpan.FromSeconds(2);

    public FanSnapshot[] Parse()
    {
        if (!OperatingSystem.IsLinux()) return [];

        // Use cached value if recent enough
        if (_cached.Length > 0 && DateTime.UtcNow - _lastParsed < CacheInterval)
            return _cached;

        try
        {
            // Strategy 1: Use lm-sensors JSON output
            var result = TryLmSensors();
            if (result.Length > 0)
            {
                _cached = result;
                _lastParsed = DateTime.UtcNow;
                return result;
            }

            // Strategy 2: Fall back to direct hwmon reading
            result = TryHwmon();
            _cached = result;
            _lastParsed = DateTime.UtcNow;
            return result;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Parse `sensors -j` JSON output to extract fan RPM values.
    /// </summary>
    private static FanSnapshot[] TryLmSensors()
    {
        try
        {
            var psi = new ProcessStartInfo("sensors", "-j")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return [];

            var json = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(3000);

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(json))
                return [];

            var fans = new List<FanSnapshot>();
            using var doc = JsonDocument.Parse(json);

            foreach (var chip in doc.RootElement.EnumerateObject())
            {
                // chip.Name = "nct6793-isa-0290", "coretemp-isa-0000", etc.
                foreach (var feature in chip.Value.EnumerateObject())
                {
                    // Skip non-object properties like "Adapter"
                    if (feature.Value.ValueKind != JsonValueKind.Object) continue;

                    // Look for fan features (e.g. "fan1", "fan2", "Chassis Fan")
                    var featureName = feature.Name;
                    bool isFan = featureName.Contains("fan", StringComparison.OrdinalIgnoreCase);

                    if (!isFan) continue;

                    // Extract the RPM value — it's the first numeric sub-key
                    foreach (var metric in feature.Value.EnumerateObject())
                    {
                        if (metric.Value.ValueKind == JsonValueKind.Number)
                        {
                            var rpm = (int)metric.Value.GetDouble();
                            fans.Add(new FanSnapshot
                            {
                                Label = featureName,
                                Rpm = rpm
                            });
                            break; // Take only the first metric (the input value)
                        }
                    }
                }
            }

            return fans.ToArray();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Fallback: read fan data directly from /sys/class/hwmon/.
    /// </summary>
    private static FanSnapshot[] TryHwmon()
    {
        var fans = new List<FanSnapshot>();
        var hwmonDir = "/sys/class/hwmon";
        if (!Directory.Exists(hwmonDir)) return [];

        foreach (var device in Directory.GetDirectories(hwmonDir))
        {
            for (int i = 1; i <= 10; i++)
            {
                var inputPath = Path.Combine(device, $"fan{i}_input");
                if (!File.Exists(inputPath)) continue;

                var rpmText = File.ReadAllText(inputPath).Trim();
                if (!int.TryParse(rpmText, out int rpm)) continue;

                var label = $"Fan {i}";
                var labelPath = Path.Combine(device, $"fan{i}_label");
                if (File.Exists(labelPath))
                {
                    label = File.ReadAllText(labelPath).Trim();
                }
                else
                {
                    var namePath = Path.Combine(device, "name");
                    if (File.Exists(namePath))
                        label = $"{File.ReadAllText(namePath).Trim()} Fan {i}";
                }

                fans.Add(new FanSnapshot { Label = label, Rpm = rpm });
            }
        }

        return fans.ToArray();
    }
}
