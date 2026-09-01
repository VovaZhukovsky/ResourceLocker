namespace ResourceLocker.Server.Interfaces;

public interface IResourceLockTcpServer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    void Dispose();
}
