# RTTSTAT POWER PROMPT — EXCEED PINGPLOTTER / WINMTR / MULTIPING

You are a senior Windows systems and networking engineer. You will upgrade **Rttstat** (this repository) until it is **strictly more capable** than the strongest consumer/pro desktop analogues **in its niche**: a local, tray-resident, no-admin Windows network-quality monitor.

This file is the single source of truth for the upgrade. Do not stop for permission. Do not ship stubs. Do not embed PingPlotter, MultiPing, WinMTR, mtr, Npcap, WinPcap, Wireshark, or any third-party EXE/installer.

---

## 0. WORK UNTIL DONE

Forbidden: “I can add traceroute next”, TODOs, NotImplemented, fake hop data, skipping tests.

Required:

1. Read the existing solution (`src/Netpulse.App`, `Core`, `Infrastructure`) and extend it. Product name is **Rttstat**. Do not rename namespaces unless required.
2. Implement every milestone below in order. After each: `dotnet build` and `dotnet test`. Fix red builds immediately.
3. Keep ICMP path **without Administrator**. TCP-connect fallback already exists; keep it.
4. When finished: `dotnet publish` self-contained `win-x64` to `C:\Users\NiLle\Desktop\Rttstat` as `Rttstat.exe`.
5. UI remains RU/EN via `I18n`. Every new string goes into both dictionaries.
6. Dark theme, tray HUD, existing dashboard/stats must keep working.

---

## 1. PRODUCT POSITION (WHAT “EXCEED” MEANS)

Analogues to beat, and how Rttstat must surpass them **for a single Windows PC user**:

| Analogue | They win today | Rttstat must beat them by |
|---|---|---|
| **PingPlotter** | Hop timeline graphs, route change, long history | Same hop-time heatmap **plus** live NIC throughput, LibreSpeed, bufferbloat grade, tray HUD with ping/loss/↓/↑, no paid license, no admin |
| **WinMTR / mtr** | Live per-hop loss/latency table | Same table **plus** persistent SQLite history, hour/day/week stats, outage log, profiles, speedtest, RU/EN |
| **MultiPing** | Many targets, alerts | Profiles already exist; add live multi-target grid, sound/toast alerts, hop drill-down per target |
| **NetWorx / TrafficMonitor** | Traffic totals | Already have live NIC; add per-day totals in stats, still keep quality metrics they lack |
| **GlassWire** | Which app eats bandwidth | **Out of scope** (needs drivers). Do not copy. |

Rttstat remains: local-only, no cloud account, no SNMP platform, no packet capture driver.

**North-star user story:** glance at tray (ms + color); one click: hops, where loss starts, bufferbloat grade, 2-minute graphs, ISP-ready CSV.

---

## 2. NON-GOALS

- Bundling or reverse-engineering PingPlotter / MultiPing / WinMTR
- Npcap / WinPcap / raw sockets that require admin for the default path
- SNMP, NetFlow, WMI empire, Kubernetes, Grafana
- Identifying processes (GlassWire)
- Ads, telemetry, auto-update phoning home
- Breaking the no-admin ICMP happy path

---

## 3. CURRENT CODE YOU MUST REUSE

- Ping: `PingEngine`, `MonitorLoopService`, profiles/targets, SQLite (`SampleRepository`, `NetpulseDb`)
- NIC: `NicSampler`
- Speed: `LibreSpeedClient` / `SpeedtestService`
- Outages: `OutageTracker`
- UI: WPF + tray (`TrayController`, `HudWindow`, `FlyoutWindow`), `Sparkline` with grid
- Stats tab: per-metric charts + tables
- Loc: `src/Netpulse.App/Loc/Loc.cs`

Add new Core projects/folders as needed: `Netpulse.Core.Routing` (or `Monitoring/Trace`).

---

## 4. FEATURE SET TO IMPLEMENT

### M1 — Continuous traceroute engine (WinMTR-class)

Implement `RouteProbe` using **Windows IP Helper** `IcmpSendEcho` / `IcmpSendEcho2` via P/Invoke (`iphlpapi.dll`, `IcmpCreateFile`). Set TTL = 1..N. No admin.

For the **active profile’s primary External (else Dns) target**, every probe round (default 1s, aligned with ping interval):

- Discover hops (until destination reply or max TTL 30)
- For each hop store: hop index, IP, optional reverse DNS (async, never block probe), sent, recv, loss %, last/avg/min/max RTT, jitter
- Destination loss is the **source of truth**. Intermediate hop loss that does **not** appear at the destination must be labeled in UI as “ICMP deprioritized (common)” — do not treat it as an outage
- TCP fallback already exists for endpoint ping; for traceroute default is ICMP. Optional later: TCP connect to :443 as “final hop only” without full TTL trace
- If ICMP traceroute is fully blocked, show banner and still keep endpoint ping

Persist:

- Table `route_hops` (round_ts, target_id, hop, ip, rtt_ms, ok)
- Table `route_summary` per minute: hop, ip, avg/min/max, loss
- Route-change event when hop IP set changes (event category `Route`)

Unit-test: loss math, “destination healthy + mid-hop loss ⇒ not outage”, route-change detection.

### M2 — Route UI tab (must exist in main window nav)

Nav item: **Маршрут / Route**

Layout:

1. Target selector (from active profile)
2. Live hop table (WinMTR columns): Hop | IP | Name | Loss% | Sent | Last | Avg | Best | Worst | Jitter | note
3. Color rows: green 0%, yellow 1–5%, orange 6–20%, red >20% **on destination**; muted warning style on intermediate-only loss
4. Sparkline per selected hop (RTT over the same 120s window as dashboard)
5. Button **Copy report** — plaintext table for ISP tickets
6. Pause follows global pause

This tab is non-negotiable. A window without hop table fails the upgrade.

### M3 — Hop timeline (PingPlotter-class)

On Route tab, second view toggle **Table | Timeline**:

- X = time (same window: 2 min live; stats page can load hour/day)
- Y = hop index
- Cell/line color = RTT or loss (user toggle)
- Route change = vertical marker + event

Do **not** use LiveCharts/OpenTK (already removed; caused extra windows). Use WPF `DrawingContext` / existing `Sparkline` patterns or a new `HopHeatmap : FrameworkElement`.

### M4 — Multi-target live grid (MultiPing-class, bounded)

Dashboard or Route side panel: **all enabled targets** of the active profile as a compact grid:

Name | Host | RTT | Loss 1m | Status | Spark

Clicking a row sets traceroute target. Cap 8 in-flight pings (already). Do not become an enterprise 500-host poller.

### M5 — Bufferbloat / loaded latency (beat PingPlotter’s “quality” story)

During LibreSpeed download and upload phases, continue ICMP pings to the External target.

Record:

- Idle baseline ping (10s before test, or last 30s rolling avg)
- Ping under download load (median)
- Ping under upload load
- Grade A–F from loaded-idle delta (use a documented scale, e.g. +0–5ms A, … +200ms+ F)
- Store on `speedtests` table: `idle_ms, down_load_ms, up_load_ms, bloat_grade`
- Show on Speed tab and Stats

Never run bufferbloat as a continuous background flood.

### M6 — Alerts that are actually useful

Extend toasts (already exist) with:

- Destination loss ≥ threshold for N consecutive probes (default N = profile outage consecutive)
- Destination RTT ≥ PingBadMs for N
- Route change
- Bufferbloat grade D–F at end of speedtest
- Optional sound (system beep), toggle in Settings, default off
- Deduplicate 10 min as today
- Mute all / quiet hours still apply

Do not alert on intermediate-only ICMP loss.

### M7 — ISP-ready export

One button **Отчёт для провайдера / ISP report**:

- Time range (same as stats bucket)
- Endpoint ping min/avg/max/loss, outage list
- Last traceroute table + route changes
- Last speedtest + bufferbloat
- UTF-8 `.txt` and `.csv`
- Filename `rttstat-report-YYYYMMDD-HHMM.txt`

This is how Rttstat beats WinMTR (no history) and PingPlotter (paywall) for a home user arguing with an ISP.

### M8 — Stats completeness

Stats tab already has ping/loss/down/up/outage charts+tables. Add:

- Route-change count in the period
- Bufferbloat grades list
- Hop loss at destination over the period (from `route_summary`)
- Keep hour/day/week filters

### M9 — Quality of life

- MOS estimate (optional, G.107 simplified from RTT+loss) on destination row — label it as estimate
- Reverse DNS off by default if it slows probes; Settings toggle
- Max TTL, probe interval in Settings
- Tray tooltip: add dest loss % if > 0
- All new UI in `I18n` RU+EN
- Dark theme
- No extra taskbar windows (ToolWindow, ShowInTaskbar=false for overlays)

---

## 5. WINDOWS ICMP IMPLEMENTATION NOTES

- P/Invoke `IcmpCreateFile`, `IcmpCloseHandle`, `IcmpSendEcho` from `iphlpapi.dll`
- `IP_OPTION_INFORMATION.Ttl` for hop discovery
- Parse reply status: success, TTL expired, dest unreachable, timeout
- Timeout per hop = profile TimeoutMs
- Do not send faster than interval; serialize TTL sweep or pipeline with a small in-flight cap (e.g. 4)
- First round may take ~TTL×timeout; show “tracing…” ; subsequent rounds only refresh known hop count + 1 extra hop to detect longer paths
- IPv4 first. IPv6: if `Icmp6CreateFile` is straightforward, add it; else IPv4-only with a note in UI
- Never require elevation for this path

Document in README: intermediate routers often lie about ICMP; destination row is what matters.

---

## 6. DATA MODEL (ADD, DON’T BREAK OLD)

SQLite migrations: bump `schema_info.version`, `CREATE TABLE IF NOT EXISTS` for new tables.

Suggested:

```sql
CREATE TABLE IF NOT EXISTS route_samples (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ts INTEGER NOT NULL,
  profile_id TEXT NOT NULL,
  target_id TEXT NOT NULL,
  hop INTEGER NOT NULL,
  ip TEXT,
  rtt_ms REAL,
  ok INTEGER NOT NULL,
  status TEXT
);
CREATE INDEX IF NOT EXISTS ix_route_ts ON route_samples(ts);

CREATE TABLE IF NOT EXISTS route_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  ts INTEGER NOT NULL,
  profile_id TEXT NOT NULL,
  target_id TEXT NOT NULL,
  message TEXT NOT NULL
);
```

Extend `speedtests` with idle/load/grade columns via `ALTER TABLE` guarded by PRAGMA table_info.

Retention: route raw samples same `rawSamplesDays` as ping.

---

## 7. UI COPY (ADD TO I18n)

Russian + English for: Route, Hop, Timeline, Table, Tracing, Dest loss, Intermediate ICMP limited, Copy report, ISP report, Bufferbloat, Grade, Idle ping, Loaded ping, Route changed, MOS (est.).

---

## 8. TESTS

xUnit:

- Destination loss vs intermediate loss policy
- Route change when hop IP list differs
- Bufferbloat grade boundaries
- TTL clamp 1–30
- Report builder contains ping + hops + outages sections (pure string/function test)

No UI automation.

---

## 9. ACCEPTANCE (ALL MUST BE TRUE)

1. Route tab shows a live hop table to 1.1.1.1 or profile External without running as admin.
2. Destination loss/latency match reality (pull WAN: dest fails; LAN ok).
3. Mid-hop 100% loss with dest OK does **not** open an outage and is visually distinguished.
4. Timeline view shows hop RTT over time; a route change logs an event.
5. Speedtest writes bufferbloat grade; Speed tab displays it.
6. ISP report file includes hops + ping stats + outages.
7. Multi-target grid shows every enabled profile target.
8. Tray HUD still ping / loss / ↓ / ↑; no extra taskbar ghosts.
9. `dotnet test` green; publish to Desktop `\Rttstat\Rttstat.exe`.
10. RU and EN both have new strings; language toggle works.

---

## 10. IMPLEMENTATION ORDER

1. P/Invoke ICMP TTL probe + models + tests  
2. Hosted tracer next to `MonitorLoopService` (or inside it, same interval)  
3. SQLite tables  
4. Route tab table  
5. Hop heatmap  
6. Bufferbloat hooks in `SpeedtestService`  
7. Alerts + ISP report  
8. Stats extras + loc  
9. Publish  

Do not skip 4. A traceroute engine with no UI is a failed delivery.

---

## 11. DEFINITION OF DONE

Rttstat is done with this prompt when a user can: leave it in the tray, open **Маршрут**, see **where** the path breaks (not only that ping is 80 ms), export a report an ISP cannot dismiss as “just ping”, and still see live Mbps without a second app.

END. Build it all now.
