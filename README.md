# Rttstat

Windows tray monitor for network health: ping, packet loss, outages, live throughput, and on-demand speed tests.

Runs in the notification area. No account, no cloud, no admin rights.

## Features

- ICMP ping to profile targets (gateway, DNS, external), TCP fallback if ICMP is blocked
- Packet loss, jitter, outage detection with duration
- Live download/upload of the selected adapter
- LibreSpeed / HTTP capacity test (button or schedule)
- Statistics by hour, day, week; event log; CSV export
- Tray HUD: ping, loss, down, up
- Profiles (home / office / VPN)
- Russian and English UI
- Portable folder or installer
- Data stays in `%AppData%\Rttstat`

## Requirements

- Windows 10 22H2 or Windows 11, x64

## Portable

Unzip and run `Rttstat.exe`. Settings go to `%AppData%\Rttstat`.  
Put an empty `portable.flag` next to the exe to keep data in `.\data`.

## Build

```bat
dotnet restore
dotnet build
dotnet test
dotnet publish src/Netpulse.App -c Release -r win-x64 --self-contained true -o publish/portable
```

Installer script: `installer/rttstat.iss` (Inno Setup 6).

## Privacy

No telemetry. Traffic is ICMP/TCP to your ping targets and HTTP to the configured speed-test server only.

## License

MIT
