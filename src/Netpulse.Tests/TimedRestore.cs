namespace Netpulse.Tests;

public sealed class TimedRestore : IDisposable
{
    private readonly List<Action> _undo = [];
    private readonly Timer _timer;
    private int _done;

    public TimedRestore(TimeSpan limit)
    {
        _timer = new Timer(_ => Finish(), null, limit, Timeout.InfiniteTimeSpan);
    }

    public void Restore(Action undo)
    {
        lock (_undo) _undo.Add(undo);
    }

    public void Dispose() => Finish();

    private void Finish()
    {
        if (Interlocked.Exchange(ref _done, 1) != 0) return;
        try { _timer.Dispose(); } catch { /* already disposed */ }
        Action[] copy;
        lock (_undo) copy = _undo.ToArray();
        for (var i = copy.Length - 1; i >= 0; i--)
        {
            try { copy[i](); } catch { /* restore must not throw out */ }
        }
    }
}
