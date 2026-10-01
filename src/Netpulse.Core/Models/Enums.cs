namespace Netpulse.Core.Models;

public enum PingProtocol
{
    Icmp,
    TcpFallbackAuto,
    TcpOnly
}

public enum TargetRole
{
    Gateway,
    Dns,
    External,
    Custom
}

public enum LinkQuality
{
    Ok,
    Warn,
    Bad,
    Down,
    Paused
}

public enum OutageCause
{
    NicDown,
    NoGateway,
    NoInternet,
    DnsFailure,
    Unknown,
    AppExit
}

public enum EventLevel
{
    Debug,
    Info,
    Warn,
    Error
}

public enum EventCategory
{
    App,
    Outage,
    Ping,
    Threshold,
    Adapter,
    Speedtest,
    Profile,
    Settings,
    Route
}

public enum SpeedtestPhase
{
    Idle,
    Pinging,
    Downloading,
    Uploading,
    Cancelling,
    Done,
    Error
}
