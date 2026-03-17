using System.Globalization;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Parses /proc/pressure/memory for PSI (Pressure Stall Information).
/// Format: some avg10=X.XX avg60=X.XX avg300=X.XX total=N
///         full avg10=X.XX avg60=X.XX avg300=X.XX total=N
/// </summary>
public sealed class PressureParser
{
    public Shared.PressureSnapshot Parse()
    {
        if (!OperatingSystem.IsLinux())
            return new Shared.PressureSnapshot();

        try
        {
            var content = File.ReadAllText("/proc/pressure/memory");
            return ParseContent(content);
        }
        catch
        {
            return new Shared.PressureSnapshot();
        }
    }

    internal static Shared.PressureSnapshot ParseContent(string content)
    {
        var result = new Shared.PressureSnapshot();
        var span = content.AsSpan();

        foreach (var rawLine in span.EnumerateLines())
        {
            var line = rawLine.Trim();
            if (line.IsEmpty) continue;

            if (line.StartsWith("some"))
            {
                ExtractPsiValues(line, out double avg10, out double avg60, out double avg300);
                result.SomeAvg10 = avg10;
                result.SomeAvg60 = avg60;
                result.SomeAvg300 = avg300;
            }
            else if (line.StartsWith("full"))
            {
                ExtractPsiValues(line, out double avg10, out double avg60, out double avg300);
                result.FullAvg10 = avg10;
                result.FullAvg60 = avg60;
                result.FullAvg300 = avg300;
            }
        }

        return result;
    }

    private static void ExtractPsiValues(ReadOnlySpan<char> line, out double avg10, out double avg60, out double avg300)
    {
        avg10 = avg60 = avg300 = 0;

        foreach (var segment in line.Split(' '))
        {
            var token = line[segment];
            if (token.StartsWith("avg10="))
                double.TryParse(token.Slice(6), NumberStyles.Float, CultureInfo.InvariantCulture, out avg10);
            else if (token.StartsWith("avg60="))
                double.TryParse(token.Slice(6), NumberStyles.Float, CultureInfo.InvariantCulture, out avg60);
            else if (token.StartsWith("avg300="))
                double.TryParse(token.Slice(7), NumberStyles.Float, CultureInfo.InvariantCulture, out avg300);
        }
    }
}
