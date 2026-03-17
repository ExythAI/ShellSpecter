using System.Globalization;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Parses /proc/net/dev and computes delta KB/s per network interface.
/// </summary>
public sealed class NetworkParser
{
    private readonly Dictionary<string, (long rxBytes, long txBytes)> _previous = new();
    private DateTime _lastRead = DateTime.MinValue;
    private bool _hasBaseline;

    public Shared.NetworkSnapshot[] Parse()
    {
        if (!OperatingSystem.IsLinux())
            return [];

        var content = File.ReadAllText("/proc/net/dev");
        return ParseContent(content);
    }

    internal Shared.NetworkSnapshot[] ParseContent(string content)
    {
        var now = DateTime.UtcNow;
        var elapsed = _hasBaseline ? (now - _lastRead).TotalSeconds : 0;
        var results = new List<Shared.NetworkSnapshot>();
        var span = content.AsSpan();
        int lineNum = 0;

        foreach (var rawLine in span.EnumerateLines())
        {
            lineNum++;
            // Skip header lines
            if (lineNum <= 2) continue;

            var line = rawLine.Trim();
            if (line.IsEmpty) continue;

            int colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;

            var ifName = line.Slice(0, colonIdx).Trim().ToString();

            // Skip loopback
            if (ifName == "lo") continue;

            var rest = line.Slice(colonIdx + 1).Trim();
            Span<long> fields = stackalloc long[16];
            int fieldIdx = 0;

            foreach (var seg in rest.Split(' '))
            {
                var token = rest[seg].Trim();
                if (token.IsEmpty) continue;
                if (fieldIdx >= 16) break;
                long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out fields[fieldIdx]);
                fieldIdx++;
            }

            long rxBytes = fields[0];
            long txBytes = fields[8];

            double rxKbps = 0, txKbps = 0;
            if (_hasBaseline && elapsed > 0 && _previous.TryGetValue(ifName, out var prev))
            {
                rxKbps = (rxBytes - prev.rxBytes) / 1024.0 / elapsed;
                txKbps = (txBytes - prev.txBytes) / 1024.0 / elapsed;
            }

            _previous[ifName] = (rxBytes, txBytes);

            results.Add(new Shared.NetworkSnapshot
            {
                InterfaceName = ifName,
                RxKbPerSec = Math.Max(0, rxKbps),
                TxKbPerSec = Math.Max(0, txKbps)
            });
        }

        _lastRead = now;
        _hasBaseline = true;
        return results.ToArray();
    }
}
