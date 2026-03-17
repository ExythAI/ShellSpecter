namespace ShellSpecter.Shared;

/// <summary>
/// Static system information — host identity, hardware summary, OS details.
/// </summary>
public sealed class SystemInfo
{
    public string HostName { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public string OperatingSystem { get; set; } = "";
    public string KernelVersion { get; set; } = "";
    public string Architecture { get; set; } = "";
    public int LogicalCores { get; set; }
    public string CpuModel { get; set; } = "";
    public long TotalRamMb { get; set; }
    public long UptimeSeconds { get; set; }
    public DiskInfo[] DiskInfos { get; set; } = [];
}

public sealed class DiskInfo
{
    public string MountPoint { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public long TotalGb { get; set; }
    public long UsedGb { get; set; }
    public long AvailableGb { get; set; }
    public double UsagePercent { get; set; }
}
