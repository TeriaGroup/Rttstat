using System.IO.Pipes;
using System.Text;

namespace Netpulse.Infrastructure.Windows;

public sealed class SingleInstance : IDisposable
{
    public const string MutexName = @"Local\Rttstat.SingleInstance";
    public const string PipeName = "Rttstat.ShowWindow";

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cts = new();
    private bool _owned;

    public SingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out _owned);
    }

    public bool IsPrimary => _owned;

    public event Action? ShowRequested;

    public void StartServer()
    {
        if (!_owned) return;
        _ = Task.Run(ListenAsync);
    }

    public static void SignalShow()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(400);
            var buf = Encoding.UTF8.GetBytes("show");
            client.Write(buf, 0, buf.Length);
        }
        catch
        {
            // primary may be exiting
        }
    }

    private async Task ListenAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(_cts.Token);
                ShowRequested?.Invoke();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(200);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (_owned)
        {
            try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
        }
        _mutex.Dispose();
        _cts.Dispose();
    }
}
