using ResourceLocker.Core.Dto;
namespace ResourceLocker.Core.Interfaces;

public interface IResourceLockStore
{
    void Set(string? key, Dto.ResourceLock? resourceLock);
    ResourceLock? Get(string? key);
    bool Remove(string? key);
    (long setCount, long getCount, long deleteCount) GetStatistics();
}
