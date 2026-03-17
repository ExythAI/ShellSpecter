using Microsoft.AspNetCore.SignalR.Client;
using ShellSpecter.Shared;

namespace ShellSpecter.Seer.Services;

/// <summary>
/// Manages connections to one or more Specter daemon nodes.
/// </summary>
public sealed class NodeManager : IAsyncDisposable
{
    private readonly AuthService _auth;
    private readonly Dictionary<string, NodeConnection> _nodes = new();

    public event Action<string, SystemSnapshot>? OnSnapshot;
    public event Action? OnConnectionStateChanged;

    public IReadOnlyDictionary<string, NodeConnection> Nodes => _nodes;

    public NodeManager(AuthService auth)
    {
        _auth = auth;
    }

    public async Task ConnectAsync(string nodeUrl)
    {
        if (_nodes.ContainsKey(nodeUrl)) return;

        var node = new NodeConnection(nodeUrl);
        _nodes[nodeUrl] = node;
        OnConnectionStateChanged?.Invoke();

        try
        {
            node.Status = ConnectionStatus.Connecting;
            OnConnectionStateChanged?.Invoke();

            var hubUrl = nodeUrl.TrimEnd('/') + "/hub/telemetry";
            node.Connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    if (_auth.IsAuthenticated)
                        options.AccessTokenProvider = () => Task.FromResult(_auth.Token);
                })
                .WithAutomaticReconnect([
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                ])
                .Build();

            node.Connection.Reconnecting += _ =>
            {
                node.Status = ConnectionStatus.Connecting;
                OnConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };

            node.Connection.Reconnected += reconnectedId =>
            {
                node.Status = ConnectionStatus.Connected;
                OnConnectionStateChanged?.Invoke();
                _ = StartStreamingAsync(node);
                return Task.CompletedTask;
            };

            node.Connection.Closed += _ =>
            {
                node.Status = ConnectionStatus.Disconnected;
                OnConnectionStateChanged?.Invoke();
                return Task.CompletedTask;
            };

            await node.Connection.StartAsync();
            node.Status = ConnectionStatus.Connected;
            OnConnectionStateChanged?.Invoke();

            _ = StartStreamingAsync(node);
        }
        catch (Exception ex)
        {
            node.Status = ConnectionStatus.Error;
            node.Error = ex.Message;
            OnConnectionStateChanged?.Invoke();
        }
    }

    private async Task StartStreamingAsync(NodeConnection node)
    {
        try
        {
            if (node.Connection == null) return;

            var stream = node.Connection.StreamAsync<SystemSnapshot>("StreamTelemetry");
            await foreach (var snapshot in stream)
            {
                node.LatestSnapshot = snapshot;
                OnSnapshot?.Invoke(node.Url, snapshot);
            }
        }
        catch (Exception ex)
        {
            node.Error = ex.Message;
            // Stream ended — could be disconnect
        }
    }

    public async Task DisconnectAsync(string nodeUrl)
    {
        if (_nodes.TryGetValue(nodeUrl, out var node))
        {
            if (node.Connection != null)
            {
                await node.Connection.DisposeAsync();
            }
            _nodes.Remove(nodeUrl);
            OnConnectionStateChanged?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var node in _nodes.Values)
        {
            if (node.Connection != null)
                await node.Connection.DisposeAsync();
        }
        _nodes.Clear();
    }
}

public sealed class NodeConnection
{
    public string Url { get; }
    public HubConnection? Connection { get; set; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Disconnected;
    public string? Error { get; set; }
    public SystemSnapshot? LatestSnapshot { get; set; }

    public NodeConnection(string url)
    {
        Url = url;
    }
}

public enum ConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}
