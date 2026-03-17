using System.Globalization;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Allocation-free parser for /proc/meminfo.
/// </summary>
public sealed class MemoryParser
{
    public Shared.MemorySnapshot Parse()
    {
        if (!OperatingSystem.IsLinux())
            return new Shared.MemorySnapshot();

        var content = File.ReadAllText("/proc/meminfo");
        return ParseContent(content);
    }

    internal static Shared.MemorySnapshot ParseContent(string content)
    {
        var span = content.AsSpan();
        var result = new Shared.MemorySnapshot();

        foreach (var rawLine in span.EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty) continue;

            if (TryExtractKb(line, "MemTotal:", out long total)) result.TotalKb = total;
            else if (TryExtractKb(line, "MemAvailable:", out long avail)) result.AvailableKb = avail;
            else if (TryExtractKb(line, "Shmem:", out long shared)) result.SharedKb = shared;
            else if (TryExtractKb(line, "Buffers:", out long buffers)) result.BuffersKb = buffers;
            else if (TryExtractKb(line, "Cached:", out long cached)) result.CachedKb = cached;
            else if (TryExtractKb(line, "SwapTotal:", out long swapTotal)) result.SwapTotalKb = swapTotal;
            else if (TryExtractKb(line, "SwapFree:", out long swapFree)) result.SwapFreeKb = swapFree;
        }

        result.UsedKb = result.TotalKb - result.AvailableKb;
        result.SwapUsedKb = result.SwapTotalKb - result.SwapFreeKb;
        return result;
    }

    private static bool TryExtractKb(ReadOnlySpan<char> line, string key, out long value)
    {
        value = 0;
        if (!line.StartsWith(key)) return false;

        var rest = line.Slice(key.Length).Trim();
        // Strip trailing "kB"
        int spaceIdx = rest.IndexOf(' ');
        if (spaceIdx > 0)
            rest = rest.Slice(0, spaceIdx);

        return long.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
