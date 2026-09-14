using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SteamVRDashOSC;

/// <summary>Publishes the dashboard state to VRChat's isOverlayOpen Bool parameter.</summary>
public sealed class OscPublisher : IDisposable
{
    // Overlay indicator parameter
    public const string AvatarParameterAddress = "/avatar/parameters/isOverlayOpen";
    // UDP Client for OSC
    private readonly UdpClient _client = new();
    // OSC address
    private readonly IPEndPoint _destination;
    // Lock to prevent races between sending and closing
    private readonly object _sendLock = new();
    // True when session closed
    private bool _disposed;

    public OscPublisher(string host, int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        _destination = new IPEndPoint(ResolveAddress(host), port);
    }

    public void Publish(DashboardSnapshot snapshot)
    {
        try
        {
            lock (_sendLock)
            {
                if (_disposed)
                {
                    return;
                }
                SendBoolean(AvatarParameterAddress, snapshot.IsOpen);
            }
        }
        catch (SocketException exception)
        {
            Console.Error.WriteLine($"Warning: Could not send OSC update: {exception.Message}");
        }
    }

    private void SendBoolean(string address, bool value)
    {
        var addressBytes = PaddedOscString(address);
        // OSC Boolean values use the T/F type tags and have no payload bytes.
        var typeTagBytes = PaddedOscString(value ? ",T" : ",F");
        var message = new byte[addressBytes.Length + typeTagBytes.Length];

        Buffer.BlockCopy(addressBytes, 0, message, 0, addressBytes.Length);
        Buffer.BlockCopy(typeTagBytes, 0, message, addressBytes.Length, typeTagBytes.Length);

        _client.Send(message, message.Length, _destination);
    }

    private static byte[] PaddedOscString(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var paddedLength = ((bytes.Length + 1 + 3) / 4) * 4;
        var result = new byte[paddedLength];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private static IPAddress ResolveAddress(string host)
    {
        if (IPAddress.TryParse(host, out var address))
        {
            return address;
        }

        return Dns.GetHostAddresses(host)
            .FirstOrDefault(address => address.AddressFamily is AddressFamily.InterNetwork)
            ?? throw new ArgumentException($"No IPv4 address was found for '{host}'.", nameof(host));
    }

    // Handles ending the OSC session and closing the overlay indicator
    public void Dispose()
    {
        lock (_sendLock)
        {
            // No-op if already closed
            if (_disposed)
                return;

            // No later Publish call can send true after the final false.
            _disposed = true;
            try
            {
                SendBoolean(AvatarParameterAddress, false);
            }
            catch (SocketException)
            {
                // Ignore if final send failed
            }
            finally
            {
                _client.Dispose();
            }
        }
    }
}
