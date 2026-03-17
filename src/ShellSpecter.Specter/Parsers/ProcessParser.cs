using System.Globalization;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Efficient process list parser — enumerates /proc/[PID]/stat for differential CPU calculations.
/// </summary>
public sealed class ProcessParser
{
    private readonly Dictionary<int, (long utime, long stime, DateTime readTime)> _previous = new();
    private readonly long _clkTck;

    public ProcessParser()
    {
        // CLK_TCK is almost always 100 on Linux
        _clkTck = 100;
    }

    public Shared.ProcessSnapshot[] Parse()
    {
        if (!OperatingSystem.IsLinux())
            return [];

        var results = new List<Shared.ProcessSnapshot>();
        var now = DateTime.UtcNow;

        try
        {
            foreach (var dir in Directory.GetDirectories("/proc"))
            {
                var dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, out int pid)) continue;

                try
                {
                    var snapshot = ParseProcess(pid, now);
                    if (snapshot != null)
                        results.Add(snapshot);
                }
                catch
                {
                    // Process may have exited; skip
                }
            }
        }
        catch
        {
            // /proc may not be fully readable
        }

        // Clean up old entries
        var activePids = new HashSet<int>(results.Select(p => p.Pid));
        foreach (var key in _previous.Keys.Where(k => !activePids.Contains(k)).ToArray())
            _previous.Remove(key);

        return results
            .OrderByDescending(p => p.CpuPercent)
            .Take(100) // Top 100 processes
            .ToArray();
    }

    private Shared.ProcessSnapshot? ParseProcess(int pid, DateTime now)
    {
        var statPath = $"/proc/{pid}/stat";
        if (!File.Exists(statPath)) return null;

        var content = File.ReadAllText(statPath);
        var span = content.AsSpan();

        // The comm field is enclosed in parentheses and may contain spaces
        int openParen = span.IndexOf('(');
        int closeParen = span.LastIndexOf(')');
        if (openParen < 0 || closeParen < 0) return null;

        var name = span.Slice(openParen + 1, closeParen - openParen - 1).ToString();
        var afterComm = span.Slice(closeParen + 2).Trim(); // Skip ") "

        // Fields after comm: state(0) ppid(1) pgrp(2) session(3) tty_nr(4) tpgid(5) flags(6)
        //   minflt(7) cminflt(8) majflt(9) cmajflt(10) utime(11) stime(12) cutime(13) cstime(14)
        //   priority(15) nice(16) num_threads(17) itrealvalue(18) starttime(19) vsize(20) rss(21)
        Span<long> fields = stackalloc long[23];
        char state = 'S';
        int fieldIdx = 0;

        foreach (var seg in afterComm.Split(' '))
        {
            var token = afterComm[seg].Trim();
            if (token.IsEmpty) continue;

            if (fieldIdx == 0)
            {
                state = token[0];
            }
            else if (fieldIdx < 23)
            {
                long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out fields[fieldIdx]);
            }
            fieldIdx++;
            if (fieldIdx >= 23) break;
        }

        long ppid = fields[1];
        long utime = fields[11];
        long stime = fields[12];
        int nice = (int)fields[16];
        long rssPages = fields[21];
        long memoryKb = rssPages * 4; // Page size = 4KB typically

        double cpuPercent = 0;
        if (_previous.TryGetValue(pid, out var prev))
        {
            var elapsed = (now - prev.readTime).TotalSeconds;
            if (elapsed > 0)
            {
                long totalDelta = (utime - prev.utime) + (stime - prev.stime);
                cpuPercent = (double)totalDelta / _clkTck / elapsed * 100.0;
                cpuPercent = Math.Max(0, Math.Min(cpuPercent, 100.0 * Environment.ProcessorCount));
            }
        }

        _previous[pid] = (utime, stime, now);

        return new Shared.ProcessSnapshot
        {
            Pid = pid,
            ParentPid = (int)ppid,
            Name = name,
            State = state,
            CpuPercent = cpuPercent,
            MemoryKb = memoryKb,
            Nice = nice
        };
    }
}
