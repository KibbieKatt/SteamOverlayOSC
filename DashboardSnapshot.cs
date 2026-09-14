namespace SteamOverlayOSC;

public enum DashboardState
{
    Open,
    Closed,
    Unavailable,
}

public enum KeyboardState
{
    Open,
    Closed,
}

public sealed record DashboardSnapshot(
    DashboardState DashboardState,
    KeyboardState KeyboardState,
    DateTimeOffset ObservedAt,
    string LogMsg)
{
    public bool IsDashboardOpen => DashboardState is DashboardState.Open;
    public bool IsKeyboardOpen => KeyboardState is KeyboardState.Open;
}
