namespace ShellSpecter.Specter.Services;

/// <summary>
/// Mock telemetry collector for development/demo on non-Linux systems.
/// Generates realistic-looking fake data.
/// </summary>
public sealed class MockTelemetryCollector : BackgroundService
{
    private readonly TelemetryBroadcaster _broadcaster;
    private readonly ILogger<MockTelemetryCollector> _logger;
    private readonly Random _rng = new();
    private readonly string _hostName;

    private readonly Parsers.SystemInfoParser _systemInfoParser = new();

    // Simulated state
    private double _cpuBase = 25;
    private double _memUsedPercent = 55;
    private readonly double[] _coreLoads;

    public MockTelemetryCollector(TelemetryBroadcaster broadcaster, ILogger<MockTelemetryCollector> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
        _hostName = Environment.MachineName;
        _coreLoads = new double[Environment.ProcessorCount];
        for (int i = 0; i < _coreLoads.Length; i++)
            _coreLoads[i] = 10 + _rng.NextDouble() * 30;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Mock telemetry collector started (demo mode)");

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var snapshot = GenerateMockSnapshot();
                await _broadcaster.BroadcastAsync(snapshot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in mock collector");
            }
        }
    }

    private Shared.SystemSnapshot GenerateMockSnapshot()
    {
        // Drift CPU load
        _cpuBase = Math.Clamp(_cpuBase + (_rng.NextDouble() - 0.48) * 5, 5, 85);
        _memUsedPercent = Math.Clamp(_memUsedPercent + (_rng.NextDouble() - 0.5) * 1, 30, 90);

        var cores = new Shared.CpuCoreSnapshot[_coreLoads.Length];
        double totalUser = 0, totalSystem = 0, totalIo = 0, totalIdle = 0;

        for (int i = 0; i < _coreLoads.Length; i++)
        {
            _coreLoads[i] = Math.Clamp(_coreLoads[i] + (_rng.NextDouble() - 0.47) * 8, 1, 98);
            double user = _coreLoads[i] * 0.65;
            double system = _coreLoads[i] * 0.25;
            double ioWait = _coreLoads[i] * 0.03 + _rng.NextDouble() * 2;
            double idle = 100 - user - system - ioWait;

            totalUser += user;
            totalSystem += system;
            totalIo += ioWait;
            totalIdle += idle;

            cores[i] = new Shared.CpuCoreSnapshot
            {
                CoreId = i,
                User = Math.Round(user, 1),
                System = Math.Round(system, 1),
                IoWait = Math.Round(ioWait, 1),
                Idle = Math.Round(Math.Max(0, idle), 1),
                FrequencyMhz = 2400 + _rng.Next(-200, 600),
                TemperatureC = 45 + _rng.NextDouble() * 20
            };
        }

        int coreCount = _coreLoads.Length;
        long totalMemKb = 65536 * 1024L; // 64 GB
        long usedMemKb = (long)(totalMemKb * _memUsedPercent / 100.0);

        return new Shared.SystemSnapshot
        {
            HostName = _hostName,
            Timestamp = DateTime.UtcNow,
            Cpu = new Shared.CpuSnapshot
            {
                Cores = cores,
                TotalUser = Math.Round(totalUser / coreCount, 1),
                TotalSystem = Math.Round(totalSystem / coreCount, 1),
                TotalIoWait = Math.Round(totalIo / coreCount, 1),
                TotalIdle = Math.Round(totalIdle / coreCount, 1)
            },
            Load = new Shared.LoadAverage
            {
                Load1 = Math.Round(_cpuBase / 20.0 * coreCount * 0.3, 2),
                Load5 = Math.Round(_cpuBase / 22.0 * coreCount * 0.3, 2),
                Load15 = Math.Round(_cpuBase / 25.0 * coreCount * 0.3, 2)
            },
            Memory = new Shared.MemorySnapshot
            {
                TotalKb = totalMemKb,
                UsedKb = usedMemKb,
                AvailableKb = totalMemKb - usedMemKb,
                SharedKb = 512 * 1024,
                BuffersKb = 256 * 1024,
                CachedKb = (long)(totalMemKb * 0.15),
                SwapTotalKb = 8192 * 1024,
                SwapUsedKb = (long)(8192 * 1024 * 0.05),
                SwapFreeKb = (long)(8192 * 1024 * 0.95)
            },
            Pressure = new Shared.PressureSnapshot
            {
                SomeAvg10 = Math.Round(_rng.NextDouble() * 3, 2),
                SomeAvg60 = Math.Round(_rng.NextDouble() * 2, 2),
                SomeAvg300 = Math.Round(_rng.NextDouble() * 1.5, 2),
                FullAvg10 = Math.Round(_rng.NextDouble() * 1, 2),
                FullAvg60 = Math.Round(_rng.NextDouble() * 0.5, 2),
                FullAvg300 = Math.Round(_rng.NextDouble() * 0.3, 2)
            },
            Gpus = GenerateMockGpus(),
            Disks = GenerateMockDisks(),
            Networks = GenerateMockNetworks(),
            Processes = GenerateMockProcesses(),
            System = _systemInfoParser.Parse()
        };
    }

    private Shared.GpuSnapshot[] GenerateMockGpus()
    {
        return
        [
            new Shared.GpuSnapshot
            {
                Index = 0,
                Name = "NVIDIA RTX 3090",
                CoreLoadPercent = Math.Round(30 + _rng.NextDouble() * 50, 1),
                VramUsedMb = 8192 + _rng.Next(-2000, 4000),
                VramTotalMb = 24576,
                PowerWatts = Math.Round(150 + _rng.NextDouble() * 200, 1),
                TemperatureC = 55 + _rng.NextDouble() * 25
            }
        ];
    }

    private Shared.DiskSnapshot[] GenerateMockDisks()
    {
        return
        [
            new Shared.DiskSnapshot
            {
                DeviceName = "nvme0n1",
                ReadKbPerSec = Math.Round(_rng.NextDouble() * 50000, 1),
                WriteKbPerSec = Math.Round(_rng.NextDouble() * 30000, 1)
            },
            new Shared.DiskSnapshot
            {
                DeviceName = "sda",
                ReadKbPerSec = Math.Round(_rng.NextDouble() * 5000, 1),
                WriteKbPerSec = Math.Round(_rng.NextDouble() * 3000, 1)
            }
        ];
    }

    private Shared.NetworkSnapshot[] GenerateMockNetworks()
    {
        return
        [
            new Shared.NetworkSnapshot
            {
                InterfaceName = "eth0",
                RxKbPerSec = Math.Round(_rng.NextDouble() * 10000, 1),
                TxKbPerSec = Math.Round(_rng.NextDouble() * 5000, 1)
            }
        ];
    }

    private Shared.ProcessSnapshot[] GenerateMockProcesses()
    {
        string[] names = ["systemd", "sshd", "nginx", "python3", "node", "postgres", "redis-server",
                          "docker", "containerd", "kubelet", "journald", "cron", "bash", "vim", "htop",
                          "rsyslogd", "dbus-daemon", "NetworkManager", "pulseaudio", "Xorg"];

        var procs = new Shared.ProcessSnapshot[20];
        for (int i = 0; i < 20; i++)
        {
            procs[i] = new Shared.ProcessSnapshot
            {
                Pid = 1000 + i * 137,
                ParentPid = i == 0 ? 0 : 1000,
                Name = names[i],
                State = 'S',
                CpuPercent = Math.Round(_rng.NextDouble() * (i < 3 ? 15 : 5), 1),
                MemoryKb = _rng.Next(1024, 512 * 1024),
                Nice = 0
            };
        }

        return procs.OrderByDescending(p => p.CpuPercent).ToArray();
    }
}
