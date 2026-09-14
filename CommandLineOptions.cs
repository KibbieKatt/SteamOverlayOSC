namespace SteamOverlayOSC;

public sealed record CommandLineOptions(
    bool ShowHelp,
    TimeSpan EventBatchInterval,
    TimeSpan ReconnectDelay,
    string OscHost,
    int OscPort)
{
    public static CommandLineOptions Parse(string[] args)
    {
        var showHelp = false;
        var eventBatchInterval = TimeSpan.FromMilliseconds(250);
        var reconnectDelay = TimeSpan.FromSeconds(2);
        var oscHost = "127.0.0.1";
        var oscPort = 9000;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            string NextValue()
            {
                if (++index >= args.Length)
                {
                    throw new ArgumentException($"{argument} requires a value.");
                }

                return args[index];
            }

            switch (argument)
            {
                case "--help" or "-h":
                    showHelp = true;
                    break;
                case "--event-ms":
                    eventBatchInterval = TimeSpan.FromMilliseconds(ParseMilliseconds(NextValue(), argument, 50, 1_000));
                    break;
                case "--reconnect-ms":
                    reconnectDelay = TimeSpan.FromMilliseconds(ParseMilliseconds(NextValue(), argument, 250, 60_000));
                    break;
                case "--osc-host":
                    oscHost = NextValue();
                    break;
                case "--osc-port":
                    oscPort = ParsePort(NextValue());
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {argument}");
            }
        }

        return new CommandLineOptions(showHelp, eventBatchInterval, reconnectDelay, oscHost, oscPort);
    }

    public static void WriteHelp()
    {
        Console.WriteLine("""
            SteamVRDashOSC - sends SteamVR dashboard state to VRChat.

            Usage:
              SteamVRDashOSC [options]

            Options:
              --event-ms <50-1000>        SteamVR event batch processing interval; default: 250
              --reconnect-ms <250-60000>  SteamVR retry interval; default: 2000
              --osc-host <host>            VRChat OSC destination; default: 127.0.0.1
              --osc-port <1-65535>         VRChat OSC port; default: 9000
              --help, -h                   Show this help

            OSC output:
              /avatar/parameters/isOverlayOpen  bool  true when the SteamVR dashboard is visible
            """);
    }

    private static int ParseMilliseconds(string value, string argument, int minimum, int maximum) =>
        int.TryParse(value, out var milliseconds) && milliseconds >= minimum && milliseconds <= maximum
            ? milliseconds
            : throw new ArgumentException($"{argument} must be an integer from {minimum} to {maximum}.");

    private static int ParsePort(string value) =>
        int.TryParse(value, out var port) && port is >= 1 and <= 65_535
            ? port
            : throw new ArgumentException("--osc-port must be an integer from 1 to 65535.");
}
