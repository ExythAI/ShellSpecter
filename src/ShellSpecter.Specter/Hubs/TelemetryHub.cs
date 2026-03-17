using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Channels;

namespace ShellSpecter.Specter.Hubs;

/// <summary>
/// SignalR hub for streaming real-time telemetry to connected Seer dashboards.
/// </summary>
[Authorize]
public sealed class TelemetryHub : Hub
{
    private readonly Services.TelemetryBroadcaster _broadcaster;

    public TelemetryHub(Services.TelemetryBroadcaster broadcaster)
    {
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Server-to-client streaming of system snapshots at 500ms intervals.
    /// </summary>
    public ChannelReader<Shared.SystemSnapshot> StreamTelemetry(CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<Shared.SystemSnapshot>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _broadcaster.Subscribe(Context.ConnectionId, channel.Writer);

        cancellationToken.Register(() => _broadcaster.Unsubscribe(Context.ConnectionId));

        return channel.Reader;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _broadcaster.Unsubscribe(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
