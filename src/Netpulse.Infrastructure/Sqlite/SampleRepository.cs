using Microsoft.Data.Sqlite;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;

namespace Netpulse.Infrastructure.Sqlite;

public sealed class SampleRepository
{
    private readonly NetpulseDb _db;

    public SampleRepository(NetpulseDb db) => _db = db;

    public void InsertPings(IReadOnlyList<PingSample> batch)
    {
        if (batch.Count == 0) return;
        lock (_db.Gate)
        {
            using var tx = _db.Connection.BeginTransaction();
            foreach (var s in batch)
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO ping_samples(ts, profile_id, target_id, rtt_ms, ok, status)
                    VALUES ($ts, $p, $t, $rtt, $ok, $st)
                    """;
                cmd.Parameters.AddWithValue("$ts", s.Ts);
                cmd.Parameters.AddWithValue("$p", s.ProfileId.ToString());
                cmd.Parameters.AddWithValue("$t", s.TargetId.ToString());
                cmd.Parameters.AddWithValue("$rtt", s.RttMs is null ? DBNull.Value : s.RttMs);
                cmd.Parameters.AddWithValue("$ok", s.Ok ? 1 : 0);
                cmd.Parameters.AddWithValue("$st", s.Status);
                cmd.ExecuteNonQuery();
                UpsertPingBucket(tx, "ping_minute", "ts_minute", Floor(s.Ts, 60_000), s);
            }
            tx.Commit();
        }
    }

    public void InsertNics(IReadOnlyList<NicSample> batch)
    {
        if (batch.Count == 0) return;
        lock (_db.Gate)
        {
            using var tx = _db.Connection.BeginTransaction();
            foreach (var s in batch)
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO nic_samples(ts, adapter_id, recv_bps, sent_bps) VALUES ($ts,$a,$r,$s)";
                cmd.Parameters.AddWithValue("$ts", s.Ts);
                cmd.Parameters.AddWithValue("$a", s.AdapterId);
                cmd.Parameters.AddWithValue("$r", s.RecvBps);
                cmd.Parameters.AddWithValue("$s", s.SentBps);
                cmd.ExecuteNonQuery();

                using var up = _db.Connection.CreateCommand();
                up.Transaction = tx;
                var minute = Floor(s.Ts, 60_000);
                up.CommandText = """
                    INSERT INTO nic_minute(ts_minute, adapter_id, avg_recv_bps, avg_sent_bps, max_recv_bps, max_sent_bps, bytes_recv, bytes_sent)
                    VALUES ($m,$a,$r,$s,$r,$s,$br,$bs)
                    ON CONFLICT(ts_minute, adapter_id) DO UPDATE SET
                      avg_recv_bps = (avg_recv_bps + excluded.avg_recv_bps) / 2.0,
                      avg_sent_bps = (avg_sent_bps + excluded.avg_sent_bps) / 2.0,
                      max_recv_bps = MAX(max_recv_bps, excluded.max_recv_bps),
                      max_sent_bps = MAX(max_sent_bps, excluded.max_sent_bps),
                      bytes_recv = bytes_recv + excluded.bytes_recv,
                      bytes_sent = bytes_sent + excluded.bytes_sent
                    """;
                up.Parameters.AddWithValue("$m", minute);
                up.Parameters.AddWithValue("$a", s.AdapterId);
                up.Parameters.AddWithValue("$r", s.RecvBps);
                up.Parameters.AddWithValue("$s", s.SentBps);
                up.Parameters.AddWithValue("$br", (long)s.RecvBps);
                up.Parameters.AddWithValue("$bs", (long)s.SentBps);
                up.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public void UpsertOutage(Outage o)
    {
        lock (_db.Gate)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO outages(id, started_ts, ended_ts, cause, profile_id, adapter_id, detail)
                VALUES ($id,$st,$en,$c,$p,$a,$d)
                ON CONFLICT(id) DO UPDATE SET ended_ts=excluded.ended_ts, cause=excluded.cause, detail=excluded.detail
                """;
            cmd.Parameters.AddWithValue("$id", o.Id.ToString());
            cmd.Parameters.AddWithValue("$st", o.StartedAtUtc.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$en", o.EndedAtUtc is null ? DBNull.Value : o.EndedAtUtc.Value.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$c", o.Cause.ToString());
            cmd.Parameters.AddWithValue("$p", o.ProfileId.ToString());
            cmd.Parameters.AddWithValue("$a", (object?)o.AdapterId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$d", o.Detail);
            cmd.ExecuteNonQuery();
        }
    }

    public void InsertEvent(AppEvent e)
    {
        lock (_db.Gate)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "INSERT INTO events(ts, level, category, message) VALUES ($ts,$l,$c,$m)";
            cmd.Parameters.AddWithValue("$ts", e.Ts);
            cmd.Parameters.AddWithValue("$l", e.Level.ToString());
            cmd.Parameters.AddWithValue("$c", e.Category.ToString());
            cmd.Parameters.AddWithValue("$m", e.Message);
            cmd.ExecuteNonQuery();
        }
    }

    public void InsertSpeedtest(SpeedtestResult r)
    {
        lock (_db.Gate)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO speedtests(id, started_ts, finished_ts, server, down_mbps, up_mbps, ping_ms, jitter_ms, bytes_down, bytes_up, error, profile_id, adapter_id)
                VALUES ($id,$st,$fn,$sv,$d,$u,$p,$j,$bd,$bu,$e,$pr,$a)
                """;
            cmd.Parameters.AddWithValue("$id", r.Id.ToString());
            cmd.Parameters.AddWithValue("$st", r.StartedAtUtc.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$fn", r.FinishedAtUtc is null ? DBNull.Value : r.FinishedAtUtc.Value.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$sv", r.ServerUrl);
            cmd.Parameters.AddWithValue("$d", (object?)r.DownloadMbps ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$u", (object?)r.UploadMbps ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$p", (object?)r.PingMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$j", (object?)r.JitterMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$bd", r.BytesDown);
            cmd.Parameters.AddWithValue("$bu", r.BytesUp);
            cmd.Parameters.AddWithValue("$e", (object?)r.Error ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$pr", (object?)r.ProfileId?.ToString() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$a", (object?)r.AdapterId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<AppEvent> QueryEvents(long fromTs, long toTs, string? search, string? category, int limit = 2000)
    {
        lock (_db.Gate)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT ts, level, category, message FROM events
                WHERE ts >= $f AND ts <= $t
                """;
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                cmd.CommandText += " AND category = $c";
            if (!string.IsNullOrWhiteSpace(search))
                cmd.CommandText += " AND message LIKE $s";
            cmd.CommandText += " ORDER BY ts DESC LIMIT $n";
            cmd.Parameters.AddWithValue("$f", fromTs);
            cmd.Parameters.AddWithValue("$t", toTs);
            cmd.Parameters.AddWithValue("$n", limit);
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                cmd.Parameters.AddWithValue("$c", category);
            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("$s", "%" + search + "%");
            var list = new List<AppEvent>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new AppEvent
                {
                    Ts = r.GetInt64(0),
                    Level = Enum.Parse<EventLevel>(r.GetString(1)),
                    Category = Enum.Parse<EventCategory>(r.GetString(2)),
                    Message = r.GetString(3)
                });
            }
            return list;
        }
    }

    public IReadOnlyList<SpeedtestResult> QuerySpeedtests(int limit = 100)
    {
        lock (_db.Gate)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT id, started_ts, finished_ts, server, down_mbps, up_mbps, ping_ms, jitter_ms, bytes_down, bytes_up, error, profile_id, adapter_id FROM speedtests ORDER BY started_ts DESC LIMIT $n";
            cmd.Parameters.AddWithValue("$n", limit);
            var list = new List<SpeedtestResult>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new SpeedtestResult
                {
                    Id = Guid.Parse(r.GetString(0)),
                    StartedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(1)),
                    FinishedAtUtc = r.IsDBNull(2) ? null : DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(2)),
                    ServerUrl = r.IsDBNull(3) ? "" : r.GetString(3),
                    DownloadMbps = r.IsDBNull(4) ? null : r.GetDouble(4),
                    UploadMbps = r.IsDBNull(5) ? null : r.GetDouble(5),
                    PingMs = r.IsDBNull(6) ? null : r.GetDouble(6),
                    JitterMs = r.IsDBNull(7) ? null : r.GetDouble(7),
                    BytesDown = r.IsDBNull(8) ? 0 : r.GetInt64(8),
                    BytesUp = r.IsDBNull(9) ? 0 : r.GetInt64(9),
                    Error = r.IsDBNull(10) ? null : r.GetString(10),
                    ProfileId = r.IsDBNull(11) ? null : Guid.Parse(r.GetString(11)),
                    AdapterId = r.IsDBNull(12) ? null : r.GetString(12)
                });
            }
            return list;
        }
    }

    public StatsRow QueryStats(long fromTs, long toTs, string? profileId)
    {
        lock (_db.Gate)
        {
            using var ping = _db.Connection.CreateCommand();
            ping.CommandText = """
                SELECT
                  SUM(ok_count), SUM(fail_count),
                  MIN(min_rtt), MAX(max_rtt),
                  CASE WHEN SUM(ok_count) = 0 THEN NULL
                       ELSE SUM(avg_rtt * ok_count) / SUM(ok_count) END
                FROM ping_minute
                WHERE ts_minute >= $f AND ts_minute <= $t
                """;
            if (!string.IsNullOrEmpty(profileId))
                ping.CommandText += " AND profile_id = $p";
            ping.Parameters.AddWithValue("$f", fromTs);
            ping.Parameters.AddWithValue("$t", toTs);
            if (!string.IsNullOrEmpty(profileId))
                ping.Parameters.AddWithValue("$p", profileId);

            long ok = 0, fail = 0;
            double? min = null, max = null, avg = null;
            using (var r = ping.ExecuteReader())
            {
                if (r.Read())
                {
                    ok = r.IsDBNull(0) ? 0 : r.GetInt64(0);
                    fail = r.IsDBNull(1) ? 0 : r.GetInt64(1);
                    min = r.IsDBNull(2) ? null : r.GetDouble(2);
                    max = r.IsDBNull(3) ? null : r.GetDouble(3);
                    avg = r.IsDBNull(4) ? null : r.GetDouble(4);
                }
            }

            using var outages = _db.Connection.CreateCommand();
            outages.CommandText = """
                SELECT COUNT(*), SUM(COALESCE(ended_ts, $now) - started_ts), MAX(COALESCE(ended_ts, $now) - started_ts)
                FROM outages WHERE started_ts >= $f AND started_ts <= $t
                """;
            if (!string.IsNullOrEmpty(profileId))
                outages.CommandText += " AND profile_id = $p";
            outages.Parameters.AddWithValue("$f", fromTs);
            outages.Parameters.AddWithValue("$t", toTs);
            outages.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (!string.IsNullOrEmpty(profileId))
                outages.Parameters.AddWithValue("$p", profileId);
            long count = 0, totalDur = 0, longest = 0;
            using (var r = outages.ExecuteReader())
            {
                if (r.Read())
                {
                    count = r.IsDBNull(0) ? 0 : r.GetInt64(0);
                    totalDur = r.IsDBNull(1) ? 0 : r.GetInt64(1);
                    longest = r.IsDBNull(2) ? 0 : r.GetInt64(2);
                }
            }

            using var nic = _db.Connection.CreateCommand();
            nic.CommandText = "SELECT SUM(bytes_recv), SUM(bytes_sent) FROM nic_minute WHERE ts_minute >= $f AND ts_minute <= $t";
            nic.Parameters.AddWithValue("$f", fromTs);
            nic.Parameters.AddWithValue("$t", toTs);
            long br = 0, bs = 0;
            using (var r = nic.ExecuteReader())
            {
                if (r.Read())
                {
                    br = r.IsDBNull(0) ? 0 : r.GetInt64(0);
                    bs = r.IsDBNull(1) ? 0 : r.GetInt64(1);
                }
            }

            using var st = _db.Connection.CreateCommand();
            st.CommandText = "SELECT COUNT(*), MAX(down_mbps), MAX(up_mbps) FROM speedtests WHERE started_ts >= $f AND started_ts <= $t";
            st.Parameters.AddWithValue("$f", fromTs);
            st.Parameters.AddWithValue("$t", toTs);
            long stN = 0;
            double? bestDown = null, bestUp = null;
            using (var r = st.ExecuteReader())
            {
                if (r.Read())
                {
                    stN = r.IsDBNull(0) ? 0 : r.GetInt64(0);
                    bestDown = r.IsDBNull(1) ? null : r.GetDouble(1);
                    bestUp = r.IsDBNull(2) ? null : r.GetDouble(2);
                }
            }

            return new StatsRow(ok, fail, LossMath.Percent((int)fail, (int)(ok + fail)), avg, min, max, count, totalDur, longest, br, bs, stN, bestDown, bestUp);
        }
    }

    public IReadOnlyList<ChartPoint> QueryPingSeries(long fromTs, long toTs, string bucket)
    {
        var table = bucket switch
        {
            "hour" => ("ping_hour", "ts_hour"),
            "day" => ("ping_day", "ts_day"),
            _ => ("ping_minute", "ts_minute")
        };
        lock (_db.Gate)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = $"SELECT {table.Item2}, SUM(avg_rtt * ok_count) / NULLIF(SUM(ok_count),0), SUM(fail_count)*100.0/NULLIF(SUM(ok_count+fail_count),0) FROM {table.Item1} WHERE {table.Item2} >= $f AND {table.Item2} <= $t GROUP BY {table.Item2} ORDER BY {table.Item2}";
            cmd.Parameters.AddWithValue("$f", fromTs);
            cmd.Parameters.AddWithValue("$t", toTs);
            var list = new List<ChartPoint>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ChartPoint(
                    r.GetInt64(0),
                    r.IsDBNull(1) ? 0 : r.GetDouble(1),
                    r.IsDBNull(2) ? 0 : r.GetDouble(2)));
            }
            return list;
        }
    }

    public void RollupHoursAndDays()
    {
        lock (_db.Gate)
        {
            using var hour = _db.Connection.CreateCommand();
            hour.CommandText = """
                INSERT INTO ping_hour(ts_hour, profile_id, target_id, avg_rtt, min_rtt, max_rtt, ok_count, fail_count)
                SELECT (ts_minute / 3600000) * 3600000, profile_id, target_id,
                       SUM(avg_rtt * ok_count) / NULLIF(SUM(ok_count),0),
                       MIN(min_rtt), MAX(max_rtt), SUM(ok_count), SUM(fail_count)
                FROM ping_minute
                GROUP BY 1, 2, 3
                ON CONFLICT(ts_hour, profile_id, target_id) DO UPDATE SET
                  avg_rtt=excluded.avg_rtt, min_rtt=excluded.min_rtt, max_rtt=excluded.max_rtt,
                  ok_count=excluded.ok_count, fail_count=excluded.fail_count
                """;
            hour.ExecuteNonQuery();

            using var day = _db.Connection.CreateCommand();
            day.CommandText = """
                INSERT INTO ping_day(ts_day, profile_id, target_id, avg_rtt, min_rtt, max_rtt, ok_count, fail_count)
                SELECT (ts_hour / 86400000) * 86400000, profile_id, target_id,
                       SUM(avg_rtt * ok_count) / NULLIF(SUM(ok_count),0),
                       MIN(min_rtt), MAX(max_rtt), SUM(ok_count), SUM(fail_count)
                FROM ping_hour
                GROUP BY 1, 2, 3
                ON CONFLICT(ts_day, profile_id, target_id) DO UPDATE SET
                  avg_rtt=excluded.avg_rtt, min_rtt=excluded.min_rtt, max_rtt=excluded.max_rtt,
                  ok_count=excluded.ok_count, fail_count=excluded.fail_count
                """;
            day.ExecuteNonQuery();
        }
    }

    public void ApplyRetention(RetentionSettings r)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        lock (_db.Gate)
        {
            ExecDelete("ping_samples", "ts", now - Days(r.RawSamplesDays));
            ExecDelete("nic_samples", "ts", now - Days(r.RawSamplesDays));
            ExecDelete("ping_minute", "ts_minute", now - Days(r.MinuteSamplesDays));
            ExecDelete("nic_minute", "ts_minute", now - Days(r.MinuteSamplesDays));
            ExecDelete("ping_hour", "ts_hour", now - Days(r.HourlySamplesDays));
            ExecDelete("ping_day", "ts_day", now - Days(r.DailySamplesDays));
            ExecDelete("events", "ts", now - Days(r.EventsDays));
        }
    }

    public void Vacuum()
    {
        lock (_db.Gate)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "PRAGMA incremental_vacuum";
            cmd.ExecuteNonQuery();
        }
    }

    private void ExecDelete(string table, string col, long cut)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM {table} WHERE {col} < $c";
        cmd.Parameters.AddWithValue("$c", cut);
        cmd.ExecuteNonQuery();
    }

    private void UpsertPingBucket(SqliteTransaction tx, string table, string col, long bucket, PingSample s)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = $"""
            INSERT INTO {table}({col}, profile_id, target_id, avg_rtt, min_rtt, max_rtt, ok_count, fail_count)
            VALUES ($b,$p,$t,$rtt,$rtt,$rtt,$ok,$fail)
            ON CONFLICT({col}, profile_id, target_id) DO UPDATE SET
              avg_rtt = CASE WHEN excluded.ok_count = 0 THEN avg_rtt
                             WHEN ok_count = 0 THEN excluded.avg_rtt
                             ELSE (COALESCE(avg_rtt,0) * ok_count + COALESCE(excluded.avg_rtt,0) * excluded.ok_count)
                                  / (ok_count + excluded.ok_count) END,
              min_rtt = CASE WHEN excluded.min_rtt IS NULL THEN min_rtt
                             WHEN min_rtt IS NULL THEN excluded.min_rtt
                             ELSE MIN(min_rtt, excluded.min_rtt) END,
              max_rtt = CASE WHEN excluded.max_rtt IS NULL THEN max_rtt
                             WHEN max_rtt IS NULL THEN excluded.max_rtt
                             ELSE MAX(max_rtt, excluded.max_rtt) END,
              ok_count = ok_count + excluded.ok_count,
              fail_count = fail_count + excluded.fail_count
            """;
        cmd.Parameters.AddWithValue("$b", bucket);
        cmd.Parameters.AddWithValue("$p", s.ProfileId.ToString());
        cmd.Parameters.AddWithValue("$t", s.TargetId.ToString());
        cmd.Parameters.AddWithValue("$rtt", s.RttMs is null ? DBNull.Value : s.RttMs);
        cmd.Parameters.AddWithValue("$ok", s.Ok ? 1 : 0);
        cmd.Parameters.AddWithValue("$fail", s.Ok ? 0 : 1);
        cmd.ExecuteNonQuery();
    }

    private static long Floor(long ts, long size) => ts / size * size;
    private static long Days(int d) => (long)TimeSpan.FromDays(d).TotalMilliseconds;
}

public readonly record struct StatsRow(
    long Ok, long Fail, double Loss, double? Avg, double? Min, double? Max,
    long OutageCount, long OutageTotalMs, long LongestMs,
    long BytesRecv, long BytesSent, long Speedtests, double? BestDown, double? BestUp);

public readonly record struct ChartPoint(long Ts, double AvgPing, double Loss);
