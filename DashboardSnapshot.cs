namespace SteamVRDashOSC;

public enum DashboardState
{
    Unavailable,
    Closed,
    Open,
}

public sealed record DashboardSnapshot(
    DashboardState State,
    DateTimeOffset ObservedAt,
    string Detail)
{
    public bool IsOpen => State is DashboardState.Open;
}
