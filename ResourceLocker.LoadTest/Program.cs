using System.Net;
using NBomber.Contracts;
using NBomber.CSharp;
using ResourceLocker.Client;

var scenario1 = Scenario.Create("lock resource on tcp-server", async context =>
    {
        using var tcpClient = new ResourceLockTcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, 8080);
        var response = await tcpClient.SetAsync(
            $"resource:{RandomInt()}",
            "1C:InfoBase",
            $"operation:{RandomInt()}",
            TimeSpan.FromSeconds(30));
        if (response == "OK")
            return Response.Ok();

        return Response.Fail();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(
        LoadSimulation.NewInject(_rate: 100, _during: TimeSpan.FromSeconds(30), _interval: TimeSpan.FromSeconds(1))
    );

var scenario2 = Scenario.Create("get lock from tcp-server", async context =>
    {
        using var tcpClient = new ResourceLockTcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, 8080);
        var response = await tcpClient.GetAsync($"resource:{RandomInt()}");
        if (response != "(nil)")
            return Response.Ok();

        return Response.Fail();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(
        LoadSimulation.NewInject(_rate: 100, _during: TimeSpan.FromSeconds(30), _interval: TimeSpan.FromSeconds(1))
    );

var scenario3 = Scenario.Create("unlock resource on tcp-server", async context =>
    {
        using var tcpClient = new ResourceLockTcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, 8080);
        var response = await tcpClient.DeleteAsync($"resource:{RandomInt()}");
        if (response == "OK")
            return Response.Ok();

        return Response.Fail();
    })
    .WithWarmUpDuration(TimeSpan.FromSeconds(10))
    .WithLoadSimulations(
        LoadSimulation.NewInject(_rate: 100, _during: TimeSpan.FromSeconds(30), _interval: TimeSpan.FromSeconds(1))
    );

NBomberRunner
    .RegisterScenarios(scenario1, scenario2, scenario3)
    .Run();

Console.WriteLine("Press any key to exit...");
Console.ReadKey();

string RandomInt()
{
    var random = new Random();
    return random.Next(0, 100).ToString();
}
