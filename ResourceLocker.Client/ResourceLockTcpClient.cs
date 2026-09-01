using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ResourceLocker.Client.Interfaces;
using ResourceLock = ResourceLocker.Core.Dto.ResourceLock;

namespace ResourceLocker.Client;

public class ResourceLockTcpClient : IResourceLockTcpClient, IDisposable
{
    private TcpClient? _client;
    private StreamReader? _reader;

    public async Task ConnectAsync(IPAddress host, int port)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, port);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
    }

    public async Task<string?> SetAsync(string resourceId, string resourceType, string operationId, TimeSpan ttl)
    {
        var resourceLock = new ResourceLock
        {
            ResourceType = resourceType,
            OperationId = operationId,
            LeaseExpiresAt = DateTimeOffset.UtcNow.Add(ttl)
        };
        var payload = JsonSerializer.Serialize(resourceLock);
        var message = $"set {resourceId} {payload};";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await _client!.Client.SendAsync(messageBytes);
        return await _reader!.ReadLineAsync();
    }

    public async Task<string?> GetAsync(string resourceId)
    {
        var message = $"get {resourceId};";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await _client!.Client.SendAsync(messageBytes, SocketFlags.None);
        return await _reader!.ReadLineAsync();
    }

    public async Task<string?> DeleteAsync(string resourceId)
    {
        var message = $"delete {resourceId};";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await _client!.Client.SendAsync(messageBytes, SocketFlags.None);
        return await _reader!.ReadLineAsync();
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
