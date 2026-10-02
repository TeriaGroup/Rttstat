# RTTSTAT CYCLE 2 — ALIGN CHARTS, ACCEPT IPV6 INPUT

Do not add screens. Fix only these defects.

1. Combined live charts plot each target by its own length, so a new target is stretched across the whole window and does not share time with the others. Right-align every series to 40 points. Missing samples on the left are NaN gaps, not zeros.

2. A pasted IPv6 address in brackets, `[2606:4700:4700::1111]`, is not a host. Strip one pair of brackets before resolve and before quick-add.

3. An IPv4-mapped address `::ffff:1.1.1.1` must be traced and pinged as IPv4.

4. `PingEngine` must use the same address choice as traceroute: literal as written, otherwise IPv4 if present, otherwise IPv6.

5. If the hop list did not change, update the numbers in place. Do not clear the grid every round.

Tests use a timer that restores registered actions. Do not start `Rttstat.exe`. Do not leave changes in the autostart key or `%AppData%\Rttstat`.
