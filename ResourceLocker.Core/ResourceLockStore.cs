using ResourceLocker.Core.Interfaces;

namespace ResourceLocker.Core;

public class ResourceLockStore : IResourceLockStore, IDisposable
{
    private long _setCount;
    private long _getCount;
    private long _deleteCount;
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private readonly Dictionary<string, byte[]> _store = new();
    private bool _isDisposed;

    public void Set(string? key, Dto.ResourceLock? resourceLock)
    {
        if (key is null && resourceLock is null)
            return;

        _lock.EnterWriteLock();
        try
        {
            _store[key!] = resourceLock is null ? Array.Empty<byte>() : resourceLock.SerializeToBinary();
            Interlocked.Increment(ref _setCount);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public Dto.ResourceLock? Get(string? key)
    {
        if (key is null)
            return null;

        _lock.EnterReadLock();
        try
        {
            var value = _store.GetValueOrDefault(key);
            Interlocked.Increment(ref _getCount);

            if (value is null)
                return null;

            var resourceLock = Dto.ResourceLock.DeserializeFromBinary(value);
            if (resourceLock.IsExpired(DateTimeOffset.UtcNow)) // надо вычищать хранилище от протухших записей
                return null;

            return resourceLock;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool Remove(string? key)
    {
        if (key is null)
            return false;
        _lock.EnterWriteLock();

        try
        {
            var result = _store.Remove(key);
            Interlocked.Increment(ref _deleteCount);
            return result;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public (long setCount, long getCount, long deleteCount) GetStatistics()
    {
        return (_setCount, _getCount, _deleteCount);
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
            _lock.Dispose();
        }

        _isDisposed = true;
    }
}
