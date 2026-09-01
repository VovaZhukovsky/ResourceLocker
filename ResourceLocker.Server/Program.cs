using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ResourceLocker.Core;
using ResourceLocker.Server;

public class Program
{
    public static readonly ActivitySource ActivitySource = new(nameof(ResourceLockTcpServer));
    public static readonly Meter Meter = new(nameof(ResourceLockTcpServer));
    public static readonly Counter<long> SetCommands =
        Meter.CreateCounter<long>("set_command_total");
    public static readonly Counter<long> GetCommands =
        Meter.CreateCounter<long>("get_command_total");
    public static readonly Counter<long> DeleteCommands =
        Meter.CreateCounter<long>("delete_command_total");

    public static readonly Histogram<double> SetCommandsHistogram =
        Meter.CreateHistogram<double>("set_command_duration_seconds");
    public static readonly Histogram<double> GetCommandsHistogram =
        Meter.CreateHistogram<double>("get_command_duration_seconds");
    public static readonly Histogram<double> DeleteCommandsHistogram =
        Meter.CreateHistogram<double>("delete_command_duration_seconds");

    public static void Main(string[] args)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(nameof(ResourceLockTcpServer));

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource(nameof(ResourceLockTcpServer))
            .AddConsoleExporter()
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(nameof(ResourceLockTcpServer))
            .AddConsoleExporter()
            .Build();

        using CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken serverToken = cts.Token;

        using var server = new ResourceLockTcpServer(8080, new ResourceLockStore());
        _ = server.StartAsync(serverToken);

        Console.WriteLine("ResourceLocker server started on port 8080. Press Enter to stop...");
        Console.ReadLine();
        cts.Cancel();
    }
}
