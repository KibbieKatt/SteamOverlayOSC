using SteamVRDashOSC;

var options = CommandLineOptions.Parse(args);

if (options.ShowHelp)
{
    CommandLineOptions.WriteHelp();
    return;
}

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

using var publisher = new OscPublisher(options.OscHost, options.OscPort);

var monitor = new SteamVRMonitor(options.PollInterval, options.ReconnectDelay);
monitor.StateChanged += snapshot =>
{
    Console.WriteLine($"{snapshot.ObservedAt:O}  {snapshot.State,-11} {snapshot.Detail}");
    publisher.Publish(snapshot);
};

Console.WriteLine("SteamVR dashboard monitor started. Press Ctrl+C to stop.");
Console.WriteLine($"VRChat OSC: udp://{options.OscHost}:{options.OscPort}{OscPublisher.AvatarParameterAddress}");

try
{
    await monitor.RunAsync(cancellationSource.Token);
}
catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
{
    // exited
}
