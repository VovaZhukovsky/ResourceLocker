using ResourceLocker.Core;

namespace ResourceLocker.Core.Tests;

public class ResourceLockStoreTests
{
    private static Dto.ResourceLock CreateLock(DateTimeOffset leaseExpiresAt) => new()
    {
        ResourceType = "1C:InfoBase",
        OperationId = "operation-1",
        LeaseExpiresAt = leaseExpiresAt
    };

    [Fact]
    public async Task Validate_Parallel_Commands_Should_Be_Valid()
    {
        var store = new ResourceLockStore();
        var future = DateTimeOffset.UtcNow.AddMinutes(5);
        var tasks = new List<Task>();
        tasks.Add(Task.Run(() => store.Set("test0", CreateLock(future))));
        tasks.Add(Task.Run(() => store.Get("test0")));
        tasks.Add(Task.Run(() => store.Set("test1", CreateLock(future))));
        tasks.Add(Task.Run(() => store.Get("test1")));
        tasks.Add(Task.Run(() => store.Set("test2", CreateLock(future))));
        tasks.Add(Task.Run(() => store.Set("test2", CreateLock(future))));
        tasks.Add(Task.Run(() => store.Get("test2")));
        await Task.WhenAll(tasks);
        Assert.True(tasks.All(task => task.IsCompletedSuccessfully));

        Assert.Equal(store.GetStatistics(), (4, 3, 0));
    }

    [Fact]
    public void Validate_Get_Returns_Lock_When_Lease_Not_Expired()
    {
        var store = new ResourceLockStore();
        store.Set("res1", CreateLock(DateTimeOffset.UtcNow.AddMinutes(5)));

        var result = store.Get("res1");

        Assert.NotNull(result);
        Assert.Equal("operation-1", result!.OperationId);
    }

    [Fact]
    public void Validate_Get_Returns_Null_When_Lease_Expired()
    {
        var store = new ResourceLockStore();
        store.Set("res1", CreateLock(DateTimeOffset.UtcNow.AddMinutes(-1)));

        var result = store.Get("res1");

        Assert.Null(result);
    }

    [Fact]
    public void Validate_Remove_Deletes_Existing_Lock()
    {
        var store = new ResourceLockStore();
        store.Set("res1", CreateLock(DateTimeOffset.UtcNow.AddMinutes(5)));

        var removed = store.Remove("res1");

        Assert.True(removed);
        Assert.Null(store.Get("res1"));
    }
}
