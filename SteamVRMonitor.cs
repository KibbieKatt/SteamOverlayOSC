using Valve.VR;
using System.Runtime.InteropServices;

namespace SteamVRDashOSC;

// Checks and maintains SteamVR overlay open state
public sealed class SteamVRMonitor : IDisposable
{
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _reconnectDelay;
    private bool _connected;
    private DashboardSnapshot? _lastSnapshot;

    public SteamVRMonitor(TimeSpan pollInterval, TimeSpan reconnectDelay)
    {
        _pollInterval = pollInterval;
        _reconnectDelay = reconnectDelay;
    }

    public event Action<DashboardSnapshot>? StateChanged;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!TryConnect(out var connectionDetail))
            {
                Publish(DashboardState.Unavailable, connectionDetail);
                await Task.Delay(_reconnectDelay, cancellationToken);
                continue;
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (IsSteamVrQuitting())
                    {
                        Publish(DashboardState.Unavailable, "SteamVR is shutting down.");
                        break;
                    }

                    // The magic sauce, checks if the overlay is open
                    var overlay = OpenVR.Overlay
                        ?? throw new InvalidOperationException("SteamVR overlay interface was lost.");
                    var isDashboardVisible = overlay.IsDashboardVisible();
                    Publish(
                        isDashboardVisible ? DashboardState.Open : DashboardState.Closed,
                        isDashboardVisible ? "SteamVR dashboard is visible." : "SteamVR dashboard is hidden.");

                    await Task.Delay(_pollInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (IsOpenVrConnectionException(exception))
            {
                Publish(DashboardState.Unavailable, $"SteamVR connection lost: {exception.Message}");
            }
            finally
            {
                Disconnect();
            }

            await Task.Delay(_reconnectDelay, cancellationToken);
        }
    }

    private static bool IsSteamVrQuitting()
    {
        var system = OpenVR.System
            ?? throw new InvalidOperationException("SteamVR system interface was lost.");
        var vrEvent = new VREvent_t();
        var eventSize = (uint)Marshal.SizeOf<VREvent_t>();

        // Bound event processing so a busy queue cannot delay polling indefinitely.
        for (var count = 0; count < 64 && system.PollNextEvent(ref vrEvent, eventSize); count++)
        {
            if ((EVREventType)vrEvent.eventType == EVREventType.VREvent_Quit)
            {
                // Give us time to disconnect before SteamVR forcibly terminates the process.
                system.AcknowledgeQuit_Exiting();
                return true;
            }
        }

        return false;
    }

    private bool TryConnect(out string detail)
    {
        if (_connected)
        {
            detail = "Connected to SteamVR.";
            return true;
        }

        try
        {
            var error = EVRInitError.None;
            var system = OpenVR.Init(ref error, EVRApplicationType.VRApplication_Background);

            if (error != EVRInitError.None || system is null)
            {
                Disconnect(forceShutdown: true);
                detail = $"SteamVR is not ready ({error}).";
                return false;
            }

            // Force creation of the overlay interface while the session is valid.
            if (OpenVR.Overlay is null)
            {
                Disconnect(forceShutdown: true);
                detail = "SteamVR did not provide the overlay interface.";
                return false;
            }

            _connected = true;
            detail = "Connected to SteamVR.";
            return true;
        }
        catch (Exception exception) when (IsOpenVrConnectionException(exception))
        {
            Disconnect(forceShutdown: true);
            detail = $"SteamVR is unavailable ({exception.Message}).";
            return false;
        }
    }

    private static bool IsOpenVrConnectionException(Exception exception) =>
        exception is DllNotFoundException
            or BadImageFormatException
            or EntryPointNotFoundException
            or InvalidOperationException
            or SEHException;

    private void Publish(DashboardState state, string detail)
    {
        var snapshot = new DashboardSnapshot(state, DateTimeOffset.UtcNow, detail);
        if (_lastSnapshot is not null &&
            _lastSnapshot.State == snapshot.State &&
            _lastSnapshot.Detail == snapshot.Detail)
        {
            return;
        }

        _lastSnapshot = snapshot;
        StateChanged?.Invoke(snapshot);
    }

    private void Disconnect(bool forceShutdown = false)
    {
        if (!_connected && !forceShutdown)
        {
            return;
        }

        try
        {
            OpenVR.Shutdown();
        }
        catch (Exception exception) when (IsOpenVrConnectionException(exception))
        {
            // Best effort shutdown
        }
        finally
        {
            _connected = false;
        }
    }

    public void Dispose() => Disconnect();
}
