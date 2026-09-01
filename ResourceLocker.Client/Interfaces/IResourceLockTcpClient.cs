using System.Net;

namespace ResourceLocker.Client.Interfaces;

public interface IResourceLockTcpClient
{
    Task ConnectAsync(IPAddress host, int port);
    Task<string?> SetAsync(string resourceId, string resourceType, string operationId, TimeSpan ttl);
    Task<string?> GetAsync(string resourceId);
    Task<string?> DeleteAsync(string resourceId);
}
