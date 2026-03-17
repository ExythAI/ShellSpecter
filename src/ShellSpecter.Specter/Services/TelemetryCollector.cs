using ShellSpecter.Specter.Parsers;
using ShellSpecter.Specter.Gpu;

namespace ShellSpecter.Specter.Services;

/// <summary>
/// Background service that collects system telemetry every 500ms and broadcasts to connected clients.
/// </summary>
public sealed class TelemetryCollector : BackgroundService
{
    private readonly TelemetryBroadcaster _broadcaster;
    private readonly GpuCollector _gpuCollector;
    private readonly ILogger<TelemetryCollector> _logger;
    private readonly string _hostName;

    // Parsers (maintain state for delta calculations)
    private readonly CpuParser _cpuParser = new();
    private readonly LoadParser _loadParser = new();
    private readonly ThermalParser _thermalParser = new();
    private readonly MemoryParser _memoryParser = new();
    private readonly PressureParser _pressureParser = new();
    private readonly DiskParser _diskParser = new();
    private readonly NetworkParser _networkParser = new();
    private readonly ProcessParser _processParser = new();
    private readonly SystemInfoParser _systemInfoParser = new();
    private readonly FanParser _fanParser = new();

    public TelemetryCollector(
        TelemetryBroadcaster broadcaster,
        GpuCollector gpuCollector,
        ILogger<TelemetryCollector> logger)
    {
        _broadcaster = broadcaster;
        _gpuCollector = gpuCollector;
        _logger = logger;
        _hostName = Environment.MachineName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetry collector started on {Host}", _hostName);

        // Initial baseline read for delta parsers
        CollectSnapshot();
        await Task.Delay(500, stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var snapshot = CollectSnapshot();
                await _broadcaster.BroadcastAsync(snapshot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error collecting telemetry");
            }
        }
    }

    private Shared.SystemSnapshot CollectSnapshot()
    {
        var cpu = _cpuParser.Parse();
        var load = _loadParser.Parse();
        _thermalParser.ApplyTemperatures(cpu.Cores);
        var memory = _memoryParser.Parse();
        var pressure = _pressureParser.Parse();
        var disks = _diskParser.Parse();
        var networks = _networkParser.Parse();
        var processes = _processParser.Parse();
        var gpus = _gpuCollector.Collect();

        return new Shared.SystemSnapshot
        {
            HostName = _hostName,
            Timestamp = DateTime.UtcNow,
            Cpu = cpu,
            Load = load,
            Memory = memory,
            Pressure = pressure,
            Gpus = gpus,
            Disks = disks,
            Networks = networks,
            Processes = processes,
            Fans = _fanParser.Parse(),
            System = _systemInfoParser.Parse()
        };
    }
}
