using Valve.VR;
using System.Runtime.InteropServices;

namespace SteamVRDashOSC;

// Checks and maintains SteamVR overlay open state
public sealed class SteamVRDashMonitor : IDisposable
{
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _reconnectDelay;
    private bool _connected;
    private DashboardSnapshot? _lastSnapshot;

    public SteamVRDashMonitor(TimeSpan pollInterval, TimeSpan reconnectDelay)
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
                    // The magic sauce, checks if the overlay is open
                    var isDashboardVisible = OpenVR.Overlay.IsDashboardVisible();
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
