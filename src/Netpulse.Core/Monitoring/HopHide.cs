namespace Netpulse.Core.Monitoring;

public static class HopHide
{
    public static string Key(Guid targetId, int hop, string? ip)
    {
        var id = targetId.ToString("N");
        if (string.IsNullOrWhiteSpace(ip) || ip == "*")
            return id + "|*|" + hop;
        return id + "|" + ip;
    }

    public static bool IsHidden(IEnumerable<string>? hidden, Guid targetId, int hop, string? ip)
    {
        if (hidden is null) return false;
        var set = hidden as ISet<string> ?? hidden.ToHashSet(StringComparer.Ordinal);
        if (set.Contains(Key(targetId, hop, ip))) return true;
        var shown = string.IsNullOrWhiteSpace(ip) ? "*" : ip;
        return set.Contains(targetId.ToString("N") + "|" + hop + "|" + shown);
    }
}
