# RTTSTAT CYCLE — FIX VERIFIED DEFECTS ONLY

Do not add screens, protocols, or scores. Fix only the defects below. Then rerun tests. Stop when the next change would be a product choice, not a bug.

## Defects

1. Metric names disappear. `DataGridColumnHeader` style sets `ContentTemplate` to `Text="{Binding}"`. Column headers are localization bindings, not strings, so the template does not show the header text. Remove that `ContentTemplate`. Keep full header strings and wide columns.

2. The loss chart lies. `RollingWindow.Spark(..., rttNotLoss: false)` plots each sample as 0 or 100. One timeout becomes a 100% spike. Plot a rolling loss percent over the last 8 samples ending at that point. One miss in eight is 12.5, not 100.

3. Hiding a hop is keyed by hop index plus IP. After the route shifts, a different address at that index is hidden. Hide a real IP by `targetId|ip`. Hide `*` by `targetId|*|hop`, because it has no address. Still honor the old `targetId|hop|ip` key so hops already hidden stay hidden.

4. `SeriesChart` tick loop can spin if the step collapses. Cap the axis at 8 ticks.

## Tests

Every new test uses a timer that stops the work and runs registered restore actions, including when the test fails or overruns. Do not start `Rttstat.exe`. Do not leave changes in `HKCU\...\Run` or `%AppData%\Rttstat`. Snapshot the autostart value and restore it from the timer, even if the test did not change it.

`dotnet test` must pass. Do not commit unless asked.
