using SteamOverlayOSC;
using System.Runtime.InteropServices;

var options = CommandLineOptions.Parse(args);

if (options.ShowHelp)
{
    CommandLineOptions.WriteHelp();
    return;
}

using var cancellationSource = new CancellationTokenSource();
using var publisher = new OscPublisher(options.OscHost, options.OscPort);
var shutdownCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

using var monitor = new SteamVRMonitor(options.EventBatchInterval, options.ReconnectDelay);
monitor.StateChanged += snapshot =>
{
    Console.WriteLine($"{snapshot.ObservedAt:O}  {snapshot.LogMsg}");
    publisher.Publish(snapshot);
};

void RequestShutdown()
{
    // Dispose OSC publisher
    publisher.Dispose();
    try
    {
        // Cancel steamvr monitor
        cancellationSource.Cancel();
    }
    catch (ObjectDisposedException)
    {
        // Ignore errors
    }
}

void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
{
    eventArgs.Cancel = true;
    RequestShutdown();
}

void OnProcessExit(object? sender, EventArgs eventArgs) => publisher.Dispose();

bool OnConsoleControl(uint controlType)
{
    // Close, logoff, or shutdown
    if (controlType is not (2 or 5 or 6))
        return false;

    RequestShutdown();
    // Wait 2 seconds to allow shutdown tasks
    shutdownCompleted.Task.Wait(TimeSpan.FromSeconds(2));
    return true;
}

ConsoleControlHandler closeHandler = OnConsoleControl;
var closeHandlerRegistered = false;

try
{
    // Handle ctrl + c
    Console.CancelKeyPress += OnCancelKeyPress;
    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    closeHandlerRegistered = SetConsoleCtrlHandler(closeHandler, true);
    if (!closeHandlerRegistered)
    {
        Console.Error.WriteLine($"Warning: Could not register console close cleanup (Windows error {Marshal.GetLastWin32Error()}).");
    }

    Console.WriteLine("SteamVR dashboard monitor started. Press Ctrl+C to stop.");
    Console.WriteLine($"VRChat OSC: udp://{options.OscHost}:{options.OscPort}{OscPublisher.AvatarParameterAddress}");

    await monitor.RunAsync(cancellationSource.Token);
}
catch (OperationCanceledException) when (cancellationSource.IsCancellationRequested)
{
    // exited
}
finally
{
    publisher.Dispose();
    shutdownCompleted.TrySetResult();
    Console.CancelKeyPress -= OnCancelKeyPress;
    AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
    if (closeHandlerRegistered)
        SetConsoleCtrlHandler(closeHandler, false);
    GC.KeepAlive(closeHandler);
}

[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool SetConsoleCtrlHandler(ConsoleControlHandler handler, [MarshalAs(UnmanagedType.Bool)] bool add);

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
[return: MarshalAs(UnmanagedType.Bool)]
delegate bool ConsoleControlHandler(uint controlType);
