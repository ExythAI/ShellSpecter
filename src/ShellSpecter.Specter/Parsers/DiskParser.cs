using System.Globalization;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Parses /proc/diskstats and computes delta KB/s between readings.
/// </summary>
public sealed class DiskParser
{
    private readonly Dictionary<string, (long readSectors, long writeSectors)> _previous = new();
    private DateTime _lastRead = DateTime.MinValue;
    private bool _hasBaseline;

    // Sector size on Linux is 512 bytes
    private const double SectorSizeKb = 512.0 / 1024.0;

    public Shared.DiskSnapshot[] Parse()
    {
        if (!OperatingSystem.IsLinux())
            return [];

        var content = File.ReadAllText("/proc/diskstats");
        return ParseContent(content);
    }

    internal Shared.DiskSnapshot[] ParseContent(string content)
    {
        var now = DateTime.UtcNow;
        var elapsed = _hasBaseline ? (now - _lastRead).TotalSeconds : 0;
        var results = new List<Shared.DiskSnapshot>();
        var span = content.AsSpan();

        foreach (var rawLine in span.EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty) continue;

            // Format: major minor name reads_completed _ sectors_read _ _ writes_completed _ sectors_written ...
            Span<long> fields = stackalloc long[14];
            var name = "";
            int fieldIdx = 0;
            int tokenIdx = 0;

            foreach (var seg in line.Split(' '))
            {
                var token = line[seg].Trim();
                if (token.IsEmpty) continue;

                if (tokenIdx == 2)
                {
                    name = token.ToString();
                }
                else if (tokenIdx >= 3)
                {
                    if (fieldIdx < 14)
                    {
                        long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out fields[fieldIdx]);
                        fieldIdx++;
                    }
                }
                tokenIdx++;
            }

            if (string.IsNullOrEmpty(name)) continue;

            // Skip partitions (only keep whole devices like sda, nvme0n1)
            if (name.Length > 0 && char.IsDigit(name[^1]) && !name.Contains("n1") && !name.StartsWith("dm-"))
                continue;

            long readSectors = fields[2];   // sectors read
            long writeSectors = fields[6];  // sectors written

            double readKbps = 0, writeKbps = 0;
            if (_hasBaseline && elapsed > 0 && _previous.TryGetValue(name, out var prev))
            {
                readKbps = (readSectors - prev.readSectors) * SectorSizeKb / elapsed;
                writeKbps = (writeSectors - prev.writeSectors) * SectorSizeKb / elapsed;
            }

            _previous[name] = (readSectors, writeSectors);

            results.Add(new Shared.DiskSnapshot
            {
                DeviceName = name,
                ReadKbPerSec = Math.Max(0, readKbps),
                WriteKbPerSec = Math.Max(0, writeKbps)
            });
        }

        _lastRead = now;
        _hasBaseline = true;
        return results.ToArray();
    }
}
