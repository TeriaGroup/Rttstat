using Microsoft.Data.Sqlite;
using Netpulse.Core.Abstractions;

namespace Netpulse.Infrastructure.Sqlite;

public sealed class NetpulseDb : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly object _gate = new();

    public NetpulseDb(IAppPaths paths)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        _conn = new SqliteConnection(cs);
        _conn.Open();
        using (var cmd = _conn.CreateCommand())
        {
            cmd.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA busy_timeout=5000;
                PRAGMA foreign_keys=ON;
                """;
            cmd.ExecuteNonQuery();
        }
        Migrate();
    }

    public SqliteConnection Connection => _conn;
    public object Gate => _gate;

    private void Migrate()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS ping_samples (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              ts INTEGER NOT NULL,
              profile_id TEXT NOT NULL,
              target_id TEXT NOT NULL,
              rtt_ms REAL NULL,
              ok INTEGER NOT NULL,
              status TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_ping_ts ON ping_samples(ts);
            CREATE INDEX IF NOT EXISTS ix_ping_profile_ts ON ping_samples(profile_id, ts);
            CREATE TABLE IF NOT EXISTS ping_minute (
              ts_minute INTEGER NOT NULL,
              profile_id TEXT NOT NULL,
              target_id TEXT NOT NULL,
              avg_rtt REAL,
              min_rtt REAL,
              max_rtt REAL,
              ok_count INTEGER NOT NULL,
              fail_count INTEGER NOT NULL,
              PRIMARY KEY (ts_minute, profile_id, target_id)
            );
            CREATE TABLE IF NOT EXISTS ping_hour (
              ts_hour INTEGER NOT NULL,
              profile_id TEXT NOT NULL,
              target_id TEXT NOT NULL,
              avg_rtt REAL,
              min_rtt REAL,
              max_rtt REAL,
              ok_count INTEGER NOT NULL,
              fail_count INTEGER NOT NULL,
              PRIMARY KEY (ts_hour, profile_id, target_id)
            );
            CREATE TABLE IF NOT EXISTS ping_day (
              ts_day INTEGER NOT NULL,
              profile_id TEXT NOT NULL,
              target_id TEXT NOT NULL,
              avg_rtt REAL,
              min_rtt REAL,
              max_rtt REAL,
              ok_count INTEGER NOT NULL,
              fail_count INTEGER NOT NULL,
              PRIMARY KEY (ts_day, profile_id, target_id)
            );
            CREATE TABLE IF NOT EXISTS nic_samples (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              ts INTEGER NOT NULL,
              adapter_id TEXT NOT NULL,
              recv_bps REAL NOT NULL,
              sent_bps REAL NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_nic_ts ON nic_samples(ts);
            CREATE TABLE IF NOT EXISTS nic_minute (
              ts_minute INTEGER NOT NULL,
              adapter_id TEXT NOT NULL,
              avg_recv_bps REAL,
              avg_sent_bps REAL,
              max_recv_bps REAL,
              max_sent_bps REAL,
              bytes_recv INTEGER,
              bytes_sent INTEGER,
              PRIMARY KEY (ts_minute, adapter_id)
            );
            CREATE TABLE IF NOT EXISTS outages (
              id TEXT PRIMARY KEY,
              started_ts INTEGER NOT NULL,
              ended_ts INTEGER NULL,
              cause TEXT NOT NULL,
              profile_id TEXT NOT NULL,
              adapter_id TEXT,
              detail TEXT
            );
            CREATE INDEX IF NOT EXISTS ix_outages_started ON outages(started_ts);
            CREATE TABLE IF NOT EXISTS events (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              ts INTEGER NOT NULL,
              level TEXT NOT NULL,
              category TEXT NOT NULL,
              message TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_events_ts ON events(ts);
            CREATE TABLE IF NOT EXISTS speedtests (
              id TEXT PRIMARY KEY,
              started_ts INTEGER NOT NULL,
              finished_ts INTEGER,
              server TEXT,
              down_mbps REAL,
              up_mbps REAL,
              ping_ms REAL,
              jitter_ms REAL,
              bytes_down INTEGER,
              bytes_up INTEGER,
              error TEXT,
              profile_id TEXT,
              adapter_id TEXT
            );
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
            """;
        cmd.ExecuteNonQuery();
        EnsureColumn("speedtests", "idle_ms", "REAL");
        EnsureColumn("speedtests", "down_load_ms", "REAL");
        EnsureColumn("speedtests", "up_load_ms", "REAL");
        EnsureColumn("speedtests", "bloat_grade", "TEXT");
        using var check = _conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM schema_info";
        var n = Convert.ToInt32(check.ExecuteScalar());
        if (n == 0)
        {
            using var ins = _conn.CreateCommand();
            ins.CommandText = "INSERT INTO schema_info(version) VALUES (1)";
            ins.ExecuteNonQuery();
        }
    }

    private void EnsureColumn(string table, string col, string type)
    {
        using var q = _conn.CreateCommand();
        q.CommandText = $"PRAGMA table_info({table})";
        using var r = q.ExecuteReader();
        while (r.Read())
        {
            if (string.Equals(r.GetString(1), col, StringComparison.OrdinalIgnoreCase))
                return;
        }
        r.Close();
        using var alter = _conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {col} {type}";
        alter.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
