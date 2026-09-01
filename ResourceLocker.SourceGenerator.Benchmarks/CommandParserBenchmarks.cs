using BenchmarkDotNet.Attributes;
using ResourceLocker.Core;

namespace ResourceLocker.SourceGenerator.Benchmarks;

[MemoryDiagnoser]
public class CommandParserBenchmarks
{
    private const string SetCommand = "set resource:1 {\"resourceType\":\"1C:InfoBase\",\"operationId\":\"operation-1\",\"leaseExpiresAt\":\"2026-09-01T00:00:00Z\"}";

    [Benchmark]
    public void ParseSetCommand()
    {
        CommandParser.Parse(SetCommand);
    }
}
