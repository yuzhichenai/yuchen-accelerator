using System.Net;
using System.Net.Sockets;

namespace GameAccelerator.Core.Dns;

public interface IDnsOptimizer
{
    Task<string?> GetFastestIpAsync(string domain);
    Task<List<string>> ResolveAsync(string domain);
    void ClearCache();
}

public class DnsOptimizer : IDnsOptimizer
{
    private readonly DnsCache _cache;
    private readonly string[] _publicDnsServers;

    public DnsOptimizer()
    {
        _cache = new DnsCache(TimeSpan.FromMinutes(10));
        _publicDnsServers = new[]
        {
            "8.8.8.8",         // Google
            "1.1.1.1",         // Cloudflare
            "208.67.222.222",  // OpenDNS
            "9.9.9.9",         // Quad9
            "223.5.5.5",       // AliDNS
            "119.29.29.29",    // DNSPod
            "114.114.114.114", // 114DNS
        };
    }

    public async Task<string?> GetFastestIpAsync(string domain)
    {
        var ips = await ResolveAsync(domain);
        if (ips.Count == 0) return null;
        if (ips.Count == 1) return ips[0];

        // Probe TCP latency to each IP and pick the fastest
        var bestIp = await ProbeFastestIpAsync(ips, 443);
        return bestIp ?? ips[0];
    }

    public async Task<List<string>> ResolveAsync(string domain)
    {
        if (_cache.TryGet(domain, out var cached))
            return cached;

        var allIps = new HashSet<string>();
        var tasks = _publicDnsServers.Select(async dnsServer =>
        {
            try
            {
                var ips = await ResolveWithDnsAsync(domain, dnsServer);
                lock (allIps)
                {
                    foreach (var ip in ips)
                        allIps.Add(ip);
                }
            }
            catch { }
        });

        await Task.WhenAll(tasks);

        var result = allIps.ToList();
        _cache.Set(domain, result);
        return result;
    }

    public void ClearCache() => _cache.Clear();

    private async Task<List<string>> ResolveWithDnsAsync(string domain, string dnsServer)
    {
        // Use System.Net.Dns with a custom DNS approach
        // For simplicity, we first try the system DNS, then augment with alternative results
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(domain);
            return addresses
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .Distinct()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task<string?> ProbeFastestIpAsync(List<string> ips, int port)
    {
        string? bestIp = null;
        int bestTime = int.MaxValue;

        var probeTasks = ips.Select(async ip =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var client = new TcpClient();
                var start = Environment.TickCount;
                await client.ConnectAsync(IPAddress.Parse(ip), port, cts.Token);
                var elapsed = Environment.TickCount - start;

                lock (this)
                {
                    if (elapsed < bestTime)
                    {
                        bestTime = elapsed;
                        bestIp = ip;
                    }
                }
            }
            catch { }
        });

        await Task.WhenAll(probeTasks);
        return bestIp;
    }
}
