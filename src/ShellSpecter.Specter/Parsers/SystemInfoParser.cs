using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ShellSpecter.Specter.Parsers;

/// <summary>
/// Collects static system information: hostname, IP, OS, kernel, CPU model, uptime, disk mounts.
/// Mostly static data refreshed at lower frequency or on first collect.
/// </summary>
public sealed class SystemInfoParser
{
    private Shared.SystemInfo? _cached;
    private DateTime _lastFullRefresh = DateTime.MinValue;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    public Shared.SystemInfo Parse()
    {
        var now = DateTime.UtcNow;

        // Full refresh every 10s (disk usage/uptime change, rest is static)
        if (_cached == null || (now - _lastFullRefresh) > RefreshInterval)
        {
            _cached = CollectFull();
            _lastFullRefresh = now;
        }
        else
        {
            // Just update uptime between full refreshes
            _cached.UptimeSeconds = ReadUptime();
        }

        return _cached;
    }

    private Shared.SystemInfo CollectFull()
    {
        var info = new Shared.SystemInfo
        {
            HostName = Environment.MachineName,
            IpAddress = GetLocalIpAddress(),
            OperatingSystem = GetOsDescription(),
            KernelVersion = GetKernelVersion(),
            Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            LogicalCores = Environment.ProcessorCount,
            CpuModel = GetCpuModel(),
            TotalRamMb = GetTotalRamMb(),
            UptimeSeconds = ReadUptime(),
            DiskInfos = GetDiskInfos()
        };
        return info;
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            // Get the first non-loopback IPv4 address
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        return addr.Address.ToString();
                }
            }
            return "127.0.0.1";
        }
        catch { return "unknown"; }
    }

    private static string GetOsDescription()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                // Try to read /etc/os-release for a nicer name
                if (File.Exists("/etc/os-release"))
                {
                    foreach (var line in File.ReadLines("/etc/os-release"))
                    {
                        if (line.StartsWith("PRETTY_NAME="))
                        {
                            return line.Substring(12).Trim('"');
                        }
                    }
                }
            }
            catch { }
        }
        return System.Runtime.InteropServices.RuntimeInformation.OSDescription;
    }

    private static string GetKernelVersion()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                if (File.Exists("/proc/version"))
                {
                    var content = File.ReadAllText("/proc/version").Trim();
                    // Extract version number: "Linux version 5.15.0-..."
                    var parts = content.Split(' ');
                    if (parts.Length >= 3) return parts[2];
                }
            }
            catch { }
        }
        return Environment.OSVersion.Version.ToString();
    }

    private static string GetCpuModel()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                if (File.Exists("/proc/cpuinfo"))
                {
                    foreach (var line in File.ReadLines("/proc/cpuinfo"))
                    {
                        if (line.StartsWith("model name"))
                        {
                            int colonIdx = line.IndexOf(':');
                            if (colonIdx > 0)
                                return line.Substring(colonIdx + 1).Trim();
                        }
                    }
                }
            }
            catch { }
        }
        return $"{Environment.ProcessorCount}-core processor";
    }

    private static long GetTotalRamMb()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                if (File.Exists("/proc/meminfo"))
                {
                    foreach (var line in File.ReadLines("/proc/meminfo"))
                    {
                        if (line.StartsWith("MemTotal:"))
                        {
                            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                                return kb / 1024;
                        }
                    }
                }
            }
            catch { }
        }

        // Fallback: use GC info (rough estimate)
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.TotalAvailableMemoryBytes / 1024 / 1024;
        }
        catch { return 0; }
    }

    private static long ReadUptime()
    {
        if (OperatingSystem.IsLinux())
        {
            try
            {
                if (File.Exists("/proc/uptime"))
                {
                    var content = File.ReadAllText("/proc/uptime").Trim();
                    var parts = content.Split(' ');
                    if (parts.Length >= 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double secs))
                        return (long)secs;
                }
            }
            catch { }
        }
        return (long)Environment.TickCount64 / 1000;
    }

    private static Shared.DiskInfo[] GetDiskInfos()
    {
        var disks = new List<Shared.DiskInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Network) continue;

                // Skip tiny pseudo-filesystems on Linux
                long totalGb = drive.TotalSize / (1024L * 1024 * 1024);
                if (totalGb < 1) continue;

                long usedGb = (drive.TotalSize - drive.AvailableFreeSpace) / (1024L * 1024 * 1024);
                long availGb = drive.AvailableFreeSpace / (1024L * 1024 * 1024);
                double usagePct = drive.TotalSize > 0 
                    ? (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100.0 
                    : 0;

                disks.Add(new Shared.DiskInfo
                {
                    MountPoint = drive.RootDirectory.FullName,
                    FileSystem = drive.DriveFormat,
                    TotalGb = totalGb,
                    UsedGb = usedGb,
                    AvailableGb = availGb,
                    UsagePercent = Math.Round(usagePct, 1)
                });
            }
        }
        catch { }
        return disks.ToArray();
    }
}
