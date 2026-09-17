using System.Diagnostics;
using System.Net.Http;
using Netpulse.Core.Models;
using Netpulse.Core.Monitoring;

namespace Netpulse.Core.Speedtest;

public sealed class LibreSpeedClient
{
    private readonly HttpClient _http;

    public LibreSpeedClient(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Rttstat/0.1");
        _http.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
    }

    public async Task<SpeedtestResult> RunAsync(
        LibreSpeedSettings settings,
        Guid? profileId,
        string? adapterId,
        IProgress<SpeedtestProgress>? progress,
        CancellationToken ct)
    {
        var result = new SpeedtestResult
        {
            StartedAtUtc = DateTimeOffset.UtcNow,
            ServerUrl = settings.ServerBaseUrl,
            ServerName = HostName(settings.ServerBaseUrl),
            ProfileId = profileId,
            AdapterId = adapterId
        };

        try
        {
            var layout = await DetectLayoutAsync(settings.ServerBaseUrl, ct);
            progress?.Report(new SpeedtestProgress(SpeedtestPhase.Pinging, 0));
            var (ping, jitter) = await MeasurePingAsync(layout, ct);
            result.PingMs = ping;
            result.JitterMs = jitter;

            progress?.Report(new SpeedtestProgress(SpeedtestPhase.Downloading, 0));
            var (downBytes, downSec) = await MeasureDownloadAsync(layout, settings, progress, ct);
            result.BytesDown = downBytes;
            result.DownloadMbps = LossMath.Mbps(downBytes, downSec);

            progress?.Report(new SpeedtestProgress(SpeedtestPhase.Uploading, 0));
            var (upBytes, upSec) = await MeasureUploadAsync(layout, settings, progress, ct);
            result.BytesUp = upBytes;
            result.UploadMbps = LossMath.Mbps(upBytes, upSec);

            result.FinishedAtUtc = DateTimeOffset.UtcNow;
            progress?.Report(new SpeedtestProgress(SpeedtestPhase.Done, result.UploadMbps ?? 0));
        }
        catch (OperationCanceledException)
        {
            result.Error = "Cancelled";
            result.FinishedAtUtc = DateTimeOffset.UtcNow;
            progress?.Report(new SpeedtestProgress(SpeedtestPhase.Idle, 0));
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.FinishedAtUtc = DateTimeOffset.UtcNow;
            progress?.Report(new SpeedtestProgress(SpeedtestPhase.Error, 0));
        }

        return result;
    }

    private async Task<ServerLayout> DetectLayoutAsync(string baseUrl, CancellationToken ct)
    {
        var root = (baseUrl ?? "").Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(root))
            root = "https://speed.cloudflare.com";

        if (root.Contains("speed.cloudflare.com", StringComparison.OrdinalIgnoreCase))
            return ServerLayout.ForCloudflare(root);

        var candidates = new[]
        {
            ("/backend/garbage.php", "/backend/empty.php", "/backend/getIP.php"),
            ("/garbage.php", "/empty.php", "/getIP.php"),
            ("/garbage", "/empty", "/getIP"),
            ("/backend/garbage", "/backend/empty", "/backend/getIP")
        };

        foreach (var (dl, empty, ip) in candidates)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, root + empty + "?cors=true&r=" + Guid.NewGuid());
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)resp.StatusCode < 400)
                    return new ServerLayout(root, root + dl, root + empty, root + ip, false);
            }
            catch
            {
                // try next layout
            }
        }

        return new ServerLayout(root, root + "/backend/garbage.php", root + "/backend/empty.php", root + "/backend/getIP.php", false);
    }

    private async Task<(double ping, double jitter)> MeasurePingAsync(ServerLayout layout, CancellationToken ct)
    {
        var samples = new List<double>();
        for (var i = 0; i < 8; i++)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            using var resp = await _http.GetAsync(layout.PingUrl + Query(layout) + "r=" + i, HttpCompletionOption.ResponseHeadersRead, ct);
            _ = resp.EnsureSuccessStatusCode();
            sw.Stop();
            if (i > 0) samples.Add(sw.Elapsed.TotalMilliseconds);
        }
        if (samples.Count == 0) return (0, 0);
        var avg = samples.Average();
        var jitter = 0.0;
        for (var i = 1; i < samples.Count; i++)
            jitter += Math.Abs(samples[i] - samples[i - 1]);
        jitter = samples.Count > 1 ? jitter / (samples.Count - 1) : 0;
        return (avg, jitter);
    }

    private async Task<(long bytes, double seconds)> MeasureDownloadAsync(
        ServerLayout layout, LibreSpeedSettings settings, IProgress<SpeedtestProgress>? progress, CancellationToken ct)
    {
        var duration = TimeSpan.FromSeconds(Math.Clamp(settings.DurationSec, 3, 30));
        var streams = Math.Clamp(settings.DownloadStreams, 1, 8);
        var started = Stopwatch.StartNew();
        long total = 0;
        var warmup = duration.TotalSeconds * 0.15;
        long cap = 200L * 1024 * 1024;
        var tasks = Enumerable.Range(0, streams).Select(async _ =>
        {
            long local = 0;
            var buf = new byte[64 * 1024];
            while (started.Elapsed < duration && local + Interlocked.Read(ref total) < cap && !ct.IsCancellationRequested)
            {
                var url = layout.Cloudflare
                    ? $"{layout.Root}/__down?bytes=25000000&r={Guid.NewGuid()}"
                    : $"{layout.DownloadUrl}?ckSize=20&r={Guid.NewGuid()}";
                using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                int n;
                while ((n = await stream.ReadAsync(buf, ct)) > 0)
                {
                    if (started.Elapsed.TotalSeconds >= warmup)
                        Interlocked.Add(ref total, n);
                    local += n;
                    if (started.Elapsed >= duration || Interlocked.Read(ref total) >= cap) break;
                }
                var mbps = LossMath.Mbps(Interlocked.Read(ref total), Math.Max(0.1, started.Elapsed.TotalSeconds - warmup));
                progress?.Report(new SpeedtestProgress(SpeedtestPhase.Downloading, mbps));
            }
        });
        await Task.WhenAll(tasks);
        var useful = Math.Max(0.1, started.Elapsed.TotalSeconds - warmup);
        return (Interlocked.Read(ref total), useful);
    }

    private async Task<(long bytes, double seconds)> MeasureUploadAsync(
        ServerLayout layout, LibreSpeedSettings settings, IProgress<SpeedtestProgress>? progress, CancellationToken ct)
    {
        var duration = TimeSpan.FromSeconds(Math.Clamp(settings.DurationSec, 3, 30));
        var streams = Math.Clamp(settings.UploadStreams, 1, 8);
        var started = Stopwatch.StartNew();
        long total = 0;
        var warmup = duration.TotalSeconds * 0.15;
        long cap = 100L * 1024 * 1024;
        var chunk = new byte[256 * 1024];
        Random.Shared.NextBytes(chunk);
        var tasks = Enumerable.Range(0, streams).Select(async _ =>
        {
            while (started.Elapsed < duration && Interlocked.Read(ref total) < cap && !ct.IsCancellationRequested)
            {
                using var content = new ByteArrayContent(chunk);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                var url = layout.Cloudflare
                    ? $"{layout.Root}/__up?r={Guid.NewGuid()}"
                    : $"{layout.EmptyUrl}?r={Guid.NewGuid()}";
                using var resp = await _http.PostAsync(url, content, ct);
                if (started.Elapsed.TotalSeconds >= warmup)
                    Interlocked.Add(ref total, chunk.Length);
                var mbps = LossMath.Mbps(Interlocked.Read(ref total), Math.Max(0.1, started.Elapsed.TotalSeconds - warmup));
                progress?.Report(new SpeedtestProgress(SpeedtestPhase.Uploading, mbps));
            }
        });
        await Task.WhenAll(tasks);
        var useful = Math.Max(0.1, started.Elapsed.TotalSeconds - warmup);
        return (Interlocked.Read(ref total), useful);
    }

    private static string Query(ServerLayout layout) => layout.Cloudflare ? "/__down?bytes=0&" : "?cors=true&";

    private static string HostName(string url)
    {
        try { return new Uri(url).Host; }
        catch { return url; }
    }

    private readonly record struct ServerLayout(string Root, string DownloadUrl, string EmptyUrl, string IpUrl, bool Cloudflare)
    {
        public string PingUrl => Cloudflare ? Root : EmptyUrl;
        public static ServerLayout ForCloudflare(string root) => new(root, root + "/__down", root + "/__up", root, true);
    }
}

public readonly record struct SpeedtestProgress(SpeedtestPhase Phase, double LiveMbps);
