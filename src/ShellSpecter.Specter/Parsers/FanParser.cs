using ShellSpecter.Shared;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Reads fan speeds from /sys/class/hwmon/ on Linux.
/// </summary>
public sealed class FanParser
{
    public FanSnapshot[] Parse()
    {
        if (!OperatingSystem.IsLinux()) return [];

        var fans = new List<FanSnapshot>();

        try
        {
            var hwmonDir = "/sys/class/hwmon";
            if (!Directory.Exists(hwmonDir)) return [];

            foreach (var device in Directory.GetDirectories(hwmonDir))
            {
                // Look for fan*_input files (fan1_input, fan2_input, etc.)
                for (int i = 1; i <= 10; i++)
                {
                    var inputPath = Path.Combine(device, $"fan{i}_input");
                    if (!File.Exists(inputPath)) continue;

                    var rpmText = File.ReadAllText(inputPath).Trim();
                    if (!int.TryParse(rpmText, out int rpm)) continue;

                    // Try to get a label (fan1_label), fall back to device name
                    var label = $"Fan {i}";
                    var labelPath = Path.Combine(device, $"fan{i}_label");
                    if (File.Exists(labelPath))
                    {
                        label = File.ReadAllText(labelPath).Trim();
                    }
                    else
                    {
                        // Try the device name
                        var namePath = Path.Combine(device, "name");
                        if (File.Exists(namePath))
                        {
                            var deviceName = File.ReadAllText(namePath).Trim();
                            label = $"{deviceName} Fan {i}";
                        }
                    }

                    fans.Add(new FanSnapshot { Label = label, Rpm = rpm });
                }
            }
        }
        catch
        {
            // hwmon data is non-critical
        }

        return fans.ToArray();
    }
}
