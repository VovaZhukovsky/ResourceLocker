using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ResourceLock = ResourceLocker.Core.Dto.ResourceLock;

namespace ResourceLocker.SourceGenerator.Benchmarks;

[MemoryDiagnoser]
public class SerializerBenchmarks
{
    [Benchmark]
    public void TestSourceGenerator()
    {
        var resourceLock = new ResourceLock
        {
            ResourceType = "1C:InfoBase",
            OperationId = "operation-1",
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var bytes = resourceLock.SerializeToBinary();
        ResourceLock.DeserializeFromBinary(bytes);
    }

    [Benchmark]
    public void TestSystemTextJson()
    {
        var resourceLock = new ResourceLock
        {
            ResourceType = "1C:InfoBase",
            OperationId = "operation-1",
            LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(resourceLock);
        JsonSerializer.Deserialize<ResourceLock>(bytes);
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<SerializerBenchmarks>();
        BenchmarkRunner.Run<CommandParserBenchmarks>();
    }
}
