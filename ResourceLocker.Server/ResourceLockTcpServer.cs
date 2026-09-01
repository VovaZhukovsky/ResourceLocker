using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using ResourceLocker.Core;
using ResourceLocker.Core.Interfaces;
using ResourceLocker.Server.Interfaces;
using ResourceLock = ResourceLocker.Core.Dto.ResourceLock;

namespace ResourceLocker.Server;

public class ResourceLockTcpServer : IResourceLockTcpServer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private bool _isDisposed;
    private readonly List<Socket> _clientSockets = new();
    private readonly IResourceLockStore _store;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxBytes = 4000;
    private int Port { get; }

    public ResourceLockTcpServer(int port, IResourceLockStore store, int maxConnections = 10)
    {
        Port = port;
        _store = store;
        _semaphore = new SemaphoreSlim(maxConnections);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, Port));
        socket.Listen();

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var clientSocket = await socket.AcceptAsync(cancellationToken);
            _clientSockets.Add(clientSocket);
            _ = Task.Run(() => ProcessClientAsync(clientSocket, cancellationToken), cancellationToken);
        }
    }

    private async Task ProcessClientAsync(Socket clientSocket, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        StringBuilder fullReceived = new StringBuilder();
        try
        {
            while (true)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(1024);
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var received = await clientSocket.ReceiveAsync(buffer);
                    if (received == 0)
                        break;

                    var encodeReceive = Encoding.UTF8.GetString(buffer, 0, received);
                    var isFullMessage = WaitFullMessage(encodeReceive, ref fullReceived);
                    int bytesCount = Encoding.UTF8.GetByteCount(fullReceived.ToString());
                    if (bytesCount > _maxBytes)
                        break;
                    if (!isFullMessage)
                        continue;

                    var response = fullReceived.ToString().AsMemory();
                    fullReceived = new StringBuilder();
                    var result = HandleCommand(CommandParser.Parse(response.Span));
                    await clientSocket.SendAsync(result);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while processing client: {ex.Message}");
        }
        finally
        {
            DisposeClientSocket(clientSocket);
            _semaphore.Release();
        }
    }

    private byte[] HandleCommand(CommandParserResponse response)
    {
        using var activity = Program.ActivitySource.StartActivity();
        var key = response.Key.ToString();
        var result = "OK\r\n";
        activity?.SetTag("command.name", response.Command.ToString());
        var watch = Stopwatch.StartNew();
        switch (response.Command.ToString().ToLowerInvariant())
        {
            case "set":
                var resourceLock = JsonSerializer.Deserialize<ResourceLock>(response.Value, JsonOptions);
                _store.Set(key, resourceLock);
                Console.WriteLine($" set {key} {response.Value}");
                watch.Stop();
                Program.SetCommands.Add(1);
                Program.SetCommandsHistogram.Record(watch.Elapsed.TotalSeconds);
                break;
            case "delete":
                _store.Remove(key);
                Console.WriteLine($" delete {key}");
                watch.Stop();
                Program.DeleteCommands.Add(1);
                Program.DeleteCommandsHistogram.Record(watch.Elapsed.TotalSeconds);
                break;
            case "get":
                var value = _store.Get(key);
                if (value == null)
                {
                    result = "(nil)\r\n";
                    break;
                }
                result = $"{JsonSerializer.Serialize(value)}\r\n";
                Console.WriteLine($"get {key} {result}");
                watch.Stop();
                Program.GetCommands.Add(1);
                Program.GetCommandsHistogram.Record(watch.Elapsed.TotalSeconds);
                break;
            default:
                result = "-ERR Unknown command\r\n";
                break;
        }
        activity?.SetTag("result.status", result);
        return Encoding.UTF8.GetBytes(result);
    }

    private bool WaitFullMessage(string encodeReceive, ref StringBuilder fullReceived)
    {
        fullReceived.Append(encodeReceive.Trim("\r\n".ToCharArray()).Trim(";".ToCharArray()));
        if (!encodeReceive.Contains(";"))
            return false;

        return true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool isManual)
    {
        if (_isDisposed)
            return;

        if (isManual)
        {
            foreach (var clientSocket in _clientSockets)
            {
                clientSocket.Dispose();
            }
        }

        _isDisposed = true;
    }

    private void DisposeClientSocket(Socket clientSocket)
    {
        clientSocket.Shutdown(SocketShutdown.Receive);
        clientSocket.Close();
    }

    ~ResourceLockTcpServer()
    {
        Dispose(false);
    }
}
