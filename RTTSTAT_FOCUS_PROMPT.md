# RTTSTAT FOCUS PROMPT — STOP ADDING, FINISH THE DIAGNOSIS

You are a senior Windows engineer finishing **Rttstat** in this repository. The product is a local, tray-resident, no-admin network-quality monitor. It already pings, stores SQLite history, traces a route, and draws loss charts. It does **not** answer the only question that matters: **is the link bad, and where — home, path, or this target?**

`RTTSTAT_POWER_PROMPT.md` is **void**. Do not implement it. Do not add tabs, scores, protocols, or products. This file is the only spec.

Work until the acceptance list at the bottom is true. Do not stop to ask permission. Do not leave TODOs, stubs, or "next I could". Do not embed PingPlotter, WinMTR, MultiPing, Npcap, WinPcap, or any third-party EXE.

---

## 0. HARD RULES

- Windows only. C# / WPF / .NET. Product name **Rttstat**. Namespaces stay `Netpulse.*`.
- No Administrator. No raw sockets. No packet capture. No cloud, telemetry, or auto-update.
- ICMP via `iphlpapi` `IcmpSendEcho` is the traceroute path. Keep existing endpoint ping and its TCP fallback. Do not start an IPv6 traceroute project. If a target is IPv6-only, show `IPv4 only` on that row and keep going.
- RU and EN. Every new user-visible string goes into both dictionaries in `src/Netpulse.App/Loc/Loc.cs`.
- Dark UI. Do not restyle the theme. Do not add a sixth navigation tab.
- After the work: `dotnet test`, then publish self-contained `win-x64` to `C:\Users\NiLle\Desktop\Rttstat`. If `Rttstat.exe` is locked, stop the `Rttstat` process and publish again. Do not commit or push unless the user asks.
- Do not delete the user's existing `%AppData%\Rttstat` database or profiles.

---

## 1. WHAT THE WINDOW MUST BECOME

Two modes only: **Monitor** and **Settings**. Delete the peer tabs Now / Route / Loss / History. Settings is one button, not a peer product.

Monitor layout, top to bottom:

1. **Header, four numbers only:** current ping, loss (1 minute), download, upload. Plus quality and online/outage duration. Pause and speed test stay in the header. Language combo leaves the header; it stays in Settings.
2. **Quick add** stays: text box, Enter, presets `1.1.1.1` `8.8.8.8` `9.9.9.9` `ya.ru` `google.com`, several hosts split on space or comma. Cap remains 32 enabled targets.
3. **Target list.** Columns only: name, current RTT, loss 5 min, mini sparkline, remove. No MOS, no σ, no sent/recv, no session loss, no IP, no min/max in this grid. Selecting a row is the whole app.
4. **One period** for the selected target: Minutes / Hours / Days / Weeks. Same control drives ping chart, loss chart, and the outage list. There is no second period combo.
5. **Selected target panel:**
   - One diagnosis line (see §4). Not a paragraph.
   - Ping chart and loss chart for **that** target.
   - A toggle on the loss chart only: `This target` / `All targets`. Combined overlay is this toggle, not a tab.
   - Route table for **that** target: hop, IP, loss, last, avg, best, worst. A note column only when the hop is ICMP-limited.
   - Outage list for the period: start, duration, cause.
   - One button: **Copy report** (clipboard). No wizard, no PDF, no second export button.
6. Header must not show jitter, MOS, min, avg, max, loss 5m, or loss 1h. Those details, if shown at all, sit under the selected target as one quiet line: min / avg / max / jitter. Not cards.

Minutes view uses **in-memory** samples (the rolling window), so the chart is not empty for the first minute. Hours / days / weeks use SQLite. Do not draw a missing bucket as 0% loss; leave a gap.

Do not rebuild the target list every tick. `Targets.Clear()` in `MainViewModel.Apply` is a bug: it flickers and drops selection. Update rows in place by `TargetId`. Add a row when a target appears, remove it when it disappears.

---

## 2. DELETE

Remove from the UI and stop calling them. Delete the dead types if nothing references them after the UI collapse.

- Navigation sections `Dashboard` as a four-chart zoo, `Route`, `Loss`, `History`. One monitor surface replaces them.
- MOS (`RoutePolicy.MosEstimate` may remain, but nothing in the UI calls it).
- σ / stddev column.
- Header cards and runs for jitter, MOS, min, avg, max, loss 5m, loss 1h.
- `HopHeatmap` and the route heat pipeline if the new UI does not show a heatmap. Do not resurrect the heatmap.
- Journal screen, `ReloadLog` as a user feature, F5 binding to the log, `Hist` command, speed-test history grid. Keep writing events and speed-test rows to SQLite. Do not build screens for them.
- Duplicate language combo in the header.
- `OpenWindowOnStart` from any UI. Autostart stays `--tray` (already in `AutostartService`). Manual launch still opens the window.
- Second default profile. `DefaultProfiles.Create` returns **Home only**. Do not delete an Office profile that already exists on disk.
- Any new score, grade, or index except the bufferbloat letter already stored on a speed-test result. Show that letter next to the last speed-test text in the header when a test has finished. No bufferbloat tab.

Do not add: per-process bandwidth, SNMP, NetFlow, packet capture, IPv6 traceroute, cloud sync, auto-update, a new charting library, more navigation.

---

## 3. ENGINE

### Ping

`MonitorLoopService` already probes enabled targets (cap 32). Keep that. Expose a per-target loss spark and RTT spark from `RollingWindow` on `TargetLiveState` (RTT spark already exists; add loss spark the same way, 40 points, 120 seconds). The Minutes chart reads these, not SQLite.

### Traceroute

`RouteEngine` currently traces the first External, else Dns, else any target, on the ping interval, and `IcmpTtl.Ping` calls `IcmpCreateFile` on **every hop**.

Change it:

- Trace **only the selected target**. The UI publishes the selected id on `MonitorHub` (`SetTraceTarget(Guid?)`). If none is selected, trace nothing.
- Interval is **8 seconds**, not the ping interval. Ping must not wait for traceroute.
- One ICMP handle per round. Add an overload that accepts the handle. Open it at the start of the round, close it in `finally`. Never open a handle per TTL.
- Still stop at the destination or max TTL (`AppSettings.MaxTtl`, clamp 5..30). Do not reverse-DNS on the probe thread. Reverse DNS stays off unless `ReverseDns` is already true, and it must not block the next hop.
- Intermediate hop loss is **not** an outage when the destination is healthy. `RoutePolicy.IntermediateLossIsOutage` stays the rule: intermediate loss counts only when destination loss is already high. `HopLiveState.IntermediateOnlyLoss` is the UI note `ICMP limited`.

### Alerts

Profile thresholds (`PingWarnMs`, `LossWarnPercent`) apply to each enabled target. Toast text includes the **target name**. Cooldown is per target, 10 minutes, not one global cooldown that suppresses every other target. Sound only if `AlertSound` is on. Do not add new notification categories.

---

## 4. DIAGNOSIS

Add `PathDiagnosis` next to `RoutePolicy`. Input: the current hop list. Output: a short code the UI localizes. No ASN lookup, no geo, no "ISP name".

Destination = last hop. Use each hop's loss percent and average RTT.

1. No hops, or destination not reached: `tracing`.
2. Destination loss **< 5%**: result `healthy`. Any earlier hop with loss ≥ 50% is `IntermediateOnlyLoss` (ICMP limited). Do not call the path bad.
3. Otherwise find the **smallest** hop index `i` such that:
   - that hop's loss ≥ 50% of destination loss, and
   - the median loss of hops `i..last` ≥ 50% of destination loss.
4. Zone from `i`:
   - `i == 1` → `home`
   - `i == last` → `destination`
   - otherwise → `path` plus the hop number
5. If no such `i`, zone is `destination` (loss is not visible on the path).
6. Separately, latency jump: among hops `2..n`, the largest `avg[i] - avg[i-1]` that is ≥ 20 ms and `dest.avg >= avg[i] - 15`. If one exists, append `jump` and the hop number. Independent of the loss zone.

UI line examples (localize):

- `Связь в норме`
- `Потери с хопа 1 — дом / роутер`
- `Потери с хопа 4 — путь`
- `Потери на назначении`
- `Скачок задержки на хопе 3`

The route grid marks ICMP-limited rows in the note column. It does not paint them as down.

---

## 5. COPY REPORT

One button copies plain text:

```
Rttstat
<local time>  <period>
Target: <name> <host> <ip>
Ping <cur>  loss 5m <pct>  <diagnosis line>

Hops
<hop>\t<ip>\t<loss>\t<avg>

Outages
<start>\t<duration>\t<cause>
```

Use the on-screen hops and the outage query already in `SampleRepository`. Empty sections say `(none)`. No file dialog.

---

## 6. TESTS

Add tests. Do not delete existing ones. `dotnet test` must pass.

`PathDiagnosis` / `RoutePolicy`:

- Dest loss 0, hop 3 loss 100 → not an outage (`IntermediateLossIsOutage` false), hop flagged ICMP-limited, diagnosis `healthy`.
- Dest loss 80, hops 1–2 loss 0, hop 3+ loss ~80 → zone `path`, hop 3.
- Dest loss 80, hop 1 already ~80 and later hops stay high → zone `home`.
- Dest loss 80, only the last hop is high → zone `destination`.
- Latency 10, 12, 80, 85 with dest 85 → jump at that hop.
- Missing samples are not 0% (a gap stays a gap; do not invent a zero).

---

## 7. FILES YOU WILL TOUCH

Read before editing. Extend; do not rewrite the solution.

- `src/Netpulse.App/MainWindow.xaml` — collapse to Monitor + Settings.
- `src/Netpulse.App/ViewModels/MainViewModel.cs` — in-place row updates, selection, one period, copy report. It is already too large; you may extract a small helper for diagnosis display and report text. Do not start a framework rewrite.
- `src/Netpulse.App/ViewModels/SectionToVis.cs` — only Monitor and Settings need to remain reachable.
- `src/Netpulse.App/Loc/Loc.cs`
- `src/Netpulse.Core/Monitoring/RouteEngine.cs`
- `src/Netpulse.Core/Monitoring/IcmpTtl.cs`
- `src/Netpulse.Core/Monitoring/MonitorHub.cs` — selected trace target.
- `src/Netpulse.Core/Monitoring/RoutePolicy.cs` and new `PathDiagnosis`.
- `src/Netpulse.Core/Monitoring/MonitorLoopService.cs` — per-target loss spark only if it is not already published.
- `src/Netpulse.Core/Profiles/DefaultProfiles.cs` — Home only for new installs.
- `src/Netpulse.Tests/` — diagnosis cases.
- Delete `src/Netpulse.App/Controls/HopHeatmap.cs` if unused after the collapse.

Settings screen keeps: language, autostart-to-tray, adapter, HUD, mute, alert sound, outage toast, threshold toast, LibreSpeed URL, save, data folder. Do not add a settings catalog.

---

## 8. ACCEPTANCE

Done only when all of these are true:

1. The window has Monitor and Settings. No Route, Loss, or History tabs.
2. Header shows ping, loss, down, upload. No MOS, no σ, no second language box.
3. Clicking a target shows its ping chart, its loss chart, its route, and the diagnosis line.
4. `All targets` is a toggle on the loss chart, not a tab.
5. Minutes charts move before a SQLite minute bucket exists.
6. Traceroute follows the selected target, waits ~8 s between rounds, and opens one ICMP handle per round.
7. A mid-path hop with 100% loss while the destination is healthy is labeled ICMP-limited and is not an outage.
8. A loss that starts at hop 1 is `home`. A loss that starts later and holds to the destination is `path`. A loss only at the end is `destination`.
9. Copy report puts that text on the clipboard.
10. Outage rows for the selected period are visible.
11. Target grid does not flicker or lose selection on each ping.
12. `dotnet test` is green.
13. `C:\Users\NiLle\Desktop\Rttstat\Rttstat.exe` is the new build.

If a step is unclear, choose the smaller behavior above. Do not invent a feature to resolve the ambiguity.
