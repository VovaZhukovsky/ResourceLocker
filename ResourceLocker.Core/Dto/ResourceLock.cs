namespace ResourceLocker.Core.Dto;

[GenerateBinarySerializer]
public partial class ResourceLock
{
    public required string ResourceType { get; set; }
    public required string OperationId { get; set; }
    public DateTimeOffset LockedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LeaseExpiresAt { get; set; }

    public bool IsExpired(DateTimeOffset now) => now >= LeaseExpiresAt;
}
