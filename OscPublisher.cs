using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SteamVRDashOSC;

/// <summary>Publishes the dashboard state to VRChat's isOverlayOpen Bool parameter.</summary>
public sealed class OscPublisher : IDisposable
{
    public const string AvatarParameterAddress = "/avatar/parameters/isOverlayOpen";

    private readonly UdpClient _client = new();
    private readonly IPEndPoint _destination;

    public OscPublisher(string host, int port)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        _destination = new IPEndPoint(ResolveAddress(host), port);
    }

    public void Publish(DashboardSnapshot snapshot)
    {
        SendBoolean(AvatarParameterAddress, snapshot.IsOpen);
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

    public void Dispose() => _client.Dispose();
}
