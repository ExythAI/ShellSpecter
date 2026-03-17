using System.Globalization;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Parses /proc/loadavg for 1, 5, and 15-minute load averages.
/// </summary>
public sealed class LoadParser
{
    public Shared.LoadAverage Parse()
    {
        if (!OperatingSystem.IsLinux())
            return new Shared.LoadAverage();

        var content = File.ReadAllText("/proc/loadavg");
        return ParseContent(content);
    }

    internal static Shared.LoadAverage ParseContent(string content)
    {
        var span = content.AsSpan().Trim();
        var result = new Shared.LoadAverage();
        int fieldIdx = 0;

        foreach (var segment in span.Split(' '))
        {
            var token = span[segment].Trim();
            if (token.IsEmpty) continue;

            switch (fieldIdx)
            {
                case 0:
                    double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var l1);
                    result.Load1 = l1;
                    break;
                case 1:
                    double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var l5);
                    result.Load5 = l5;
                    break;
                case 2:
                    double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var l15);
                    result.Load15 = l15;
                    break;
            }
            fieldIdx++;
            if (fieldIdx > 2) break;
        }

        return result;
    }
}
