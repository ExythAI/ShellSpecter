using System.Globalization;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Allocation-free parser for /proc/stat — computes per-core CPU percentages via jiffies deltas.
/// </summary>
public sealed class CpuParser
{
    private readonly record struct CpuJiffies(long User, long Nice, long System, long Idle, long IoWait, long Irq, long SoftIrq, long Steal);

    private Dictionary<int, CpuJiffies> _previousJiffies = new();
    private CpuJiffies _previousTotal;
    private bool _hasBaseline;

    public Shared.CpuSnapshot Parse()
    {
        if (!OperatingSystem.IsLinux())
            return new Shared.CpuSnapshot();

        var content = File.ReadAllText("/proc/stat");
        return ParseContent(content);
    }

    internal Shared.CpuSnapshot ParseContent(string content)
    {
        var span = content.AsSpan();
        var cores = new List<Shared.CpuCoreSnapshot>();
        Shared.CpuSnapshot result = new();

        double totalUser = 0, totalSystem = 0, totalIoWait = 0, totalIdle = 0;

        foreach (var rawLine in span.EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty) continue;

            // Total CPU line: "cpu  ..."
            if (line.StartsWith("cpu "))
            {
                var jiffies = ParseJiffies(line.Slice(4));
                var (user, system, ioWait, idle) = ComputeDelta(-1, jiffies, ref _previousTotal);
                totalUser = user;
                totalSystem = system;
                totalIoWait = ioWait;
                totalIdle = idle;
            }
            // Per-core: "cpu0 ..."
            else if (line.StartsWith("cpu") && line.Length > 3 && char.IsDigit(line[3]))
            {
                int spaceIdx = line.IndexOf(' ');
                if (spaceIdx < 0) continue;

                var coreIdSpan = line.Slice(3, spaceIdx - 3);
                if (!int.TryParse(coreIdSpan, out int coreId)) continue;

                var jiffies = ParseJiffies(line.Slice(spaceIdx + 1));

                if (!_previousJiffies.TryGetValue(coreId, out var prevCore))
                    prevCore = default;

                var (user, system, ioWait, idle) = ComputeDelta(coreId, jiffies, ref prevCore);
                _previousJiffies[coreId] = prevCore;

                cores.Add(new Shared.CpuCoreSnapshot
                {
                    CoreId = coreId,
                    User = user,
                    System = system,
                    IoWait = ioWait,
                    Idle = idle,
                    FrequencyMhz = ReadFrequency(coreId),
                    TemperatureC = 0 // Populated by ThermalParser
                });
            }
        }

        _hasBaseline = true;

        result.Cores = cores.ToArray();
        result.TotalUser = totalUser;
        result.TotalSystem = totalSystem;
        result.TotalIoWait = totalIoWait;
        result.TotalIdle = totalIdle;
        return result;
    }

    private static CpuJiffies ParseJiffies(ReadOnlySpan<char> values)
    {
        Span<long> fields = stackalloc long[8];
        int fieldIdx = 0;

        foreach (var segment in values.Split(' '))
        {
            var token = values[segment].Trim();
            if (token.IsEmpty) continue;
            if (fieldIdx >= 8) break;
            long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out fields[fieldIdx]);
            fieldIdx++;
        }

        return new CpuJiffies(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6], fields[7]);
    }

    private (double user, double system, double ioWait, double idle) ComputeDelta(int coreId, CpuJiffies current, ref CpuJiffies previous)
    {
        if (!_hasBaseline)
        {
            if (coreId == -1)
                _previousTotal = current;
            else
                _previousJiffies[coreId] = current;
            previous = current;
            return (0, 0, 0, 100);
        }

        long prevTotal = previous.User + previous.Nice + previous.System + previous.Idle + previous.IoWait + previous.Irq + previous.SoftIrq + previous.Steal;
        long curTotal = current.User + current.Nice + current.System + current.Idle + current.IoWait + current.Irq + current.SoftIrq + current.Steal;
        long delta = curTotal - prevTotal;

        if (delta <= 0)
        {
            previous = current;
            return (0, 0, 0, 100);
        }

        double user = (double)((current.User + current.Nice) - (previous.User + previous.Nice)) / delta * 100.0;
        double system = (double)((current.System + current.Irq + current.SoftIrq) - (previous.System + previous.Irq + previous.SoftIrq)) / delta * 100.0;
        double ioWait = (double)(current.IoWait - previous.IoWait) / delta * 100.0;
        double idle = (double)(current.Idle - previous.Idle) / delta * 100.0;

        if (coreId == -1)
            _previousTotal = current;
        else
            _previousJiffies[coreId] = current;
        previous = current;

        return (Math.Max(0, user), Math.Max(0, system), Math.Max(0, ioWait), Math.Max(0, idle));
    }

    private static double ReadFrequency(int coreId)
    {
        try
        {
            var path = $"/sys/devices/system/cpu/cpu{coreId}/cpufreq/scaling_cur_freq";
            if (!File.Exists(path)) return 0;
            var text = File.ReadAllText(path).Trim();
            if (long.TryParse(text, out long khz))
                return khz / 1000.0; // Convert KHz to MHz
            return 0;
        }
        catch { return 0; }
    }
}
