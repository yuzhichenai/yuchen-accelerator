using System.Collections.Concurrent;

namespace GameAccelerator.Core.Dns;

public class DnsCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _ttl;

    public DnsCache(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    public bool TryGet(string domain, out List<string> ips)
    {
        ips = new();
        if (_cache.TryGetValue(domain, out var entry))
        {
            if (DateTime.UtcNow - entry.Timestamp < _ttl)
            {
                ips = entry.Ips;
                return true;
            }
            _cache.TryRemove(domain, out _);
        }
        return false;
    }

    public void Set(string domain, List<string> ips)
    {
        _cache[domain] = new CacheEntry { Ips = ips, Timestamp = DateTime.UtcNow };
    }

    public void Clear()
    {
        _cache.Clear();
    }

    private class CacheEntry
    {
        public List<string> Ips { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }
}
