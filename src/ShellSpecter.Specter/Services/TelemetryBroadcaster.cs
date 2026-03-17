using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ShellSpecter.Specter.Services;

/// <summary>
/// Manages subscriber channels for broadcasting telemetry snapshots.
/// </summary>
public sealed class TelemetryBroadcaster
{
    private readonly ConcurrentDictionary<string, ChannelWriter<Shared.SystemSnapshot>> _subscribers = new();

    public void Subscribe(string connectionId, ChannelWriter<Shared.SystemSnapshot> writer)
    {
        _subscribers[connectionId] = writer;
    }

    public void Unsubscribe(string connectionId)
    {
        if (_subscribers.TryRemove(connectionId, out var writer))
        {
            writer.TryComplete();
        }
    }

    public async Task BroadcastAsync(Shared.SystemSnapshot snapshot)
    {
        foreach (var kvp in _subscribers)
        {
            try
            {
                if (!kvp.Value.TryWrite(snapshot))
                {
                    // Channel full — DropOldest handles this, but just in case
                    await kvp.Value.WriteAsync(snapshot);
                }
            }
            catch
            {
                // Client disconnected — clean up next cycle
                _subscribers.TryRemove(kvp.Key, out _);
            }
        }
    }
}
