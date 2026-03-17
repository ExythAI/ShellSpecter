namespace ShellSpecter.Shared;

/// <summary>
/// Top-level telemetry payload sent per tick from the Specter daemon.
/// </summary>
public sealed class SystemSnapshot
{
    public string HostName { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public CpuSnapshot Cpu { get; set; } = new();
    public LoadAverage Load { get; set; } = new();
    public MemorySnapshot Memory { get; set; } = new();
    public PressureSnapshot Pressure { get; set; } = new();
    public GpuSnapshot[] Gpus { get; set; } = [];
    public DiskSnapshot[] Disks { get; set; } = [];
    public NetworkSnapshot[] Networks { get; set; } = [];
    public ProcessSnapshot[] Processes { get; set; } = [];
    public SystemInfo System { get; set; } = new();
}

public sealed class CpuSnapshot
{
    public CpuCoreSnapshot[] Cores { get; set; } = [];
    public double TotalUser { get; set; }
    public double TotalSystem { get; set; }
    public double TotalIoWait { get; set; }
    public double TotalIdle { get; set; }
}

public sealed class CpuCoreSnapshot
{
    public int CoreId { get; set; }
    public double User { get; set; }
    public double System { get; set; }
    public double IoWait { get; set; }
    public double Idle { get; set; }
    public double FrequencyMhz { get; set; }
    public double TemperatureC { get; set; }
}

public sealed class LoadAverage
{
    public double Load1 { get; set; }
    public double Load5 { get; set; }
    public double Load15 { get; set; }
}

public sealed class MemorySnapshot
{
    public long TotalKb { get; set; }
    public long UsedKb { get; set; }
    public long SharedKb { get; set; }
    public long BuffersKb { get; set; }
    public long CachedKb { get; set; }
    public long AvailableKb { get; set; }
    public long SwapTotalKb { get; set; }
    public long SwapUsedKb { get; set; }
    public long SwapFreeKb { get; set; }
}

public sealed class PressureSnapshot
{
    public double SomeAvg10 { get; set; }
    public double SomeAvg60 { get; set; }
    public double SomeAvg300 { get; set; }
    public double FullAvg10 { get; set; }
    public double FullAvg60 { get; set; }
    public double FullAvg300 { get; set; }
}

public sealed class GpuSnapshot
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public double CoreLoadPercent { get; set; }
    public long VramUsedMb { get; set; }
    public long VramTotalMb { get; set; }
    public double PowerWatts { get; set; }
    public double TemperatureC { get; set; }
}

public sealed class DiskSnapshot
{
    public string DeviceName { get; set; } = "";
    public double ReadKbPerSec { get; set; }
    public double WriteKbPerSec { get; set; }
}

public sealed class NetworkSnapshot
{
    public string InterfaceName { get; set; } = "";
    public double RxKbPerSec { get; set; }
    public double TxKbPerSec { get; set; }
}

public sealed class ProcessSnapshot
{
    public int Pid { get; set; }
    public int ParentPid { get; set; }
    public string Name { get; set; } = "";
    public char State { get; set; }
    public double CpuPercent { get; set; }
    public long MemoryKb { get; set; }
    public int Nice { get; set; }
}
