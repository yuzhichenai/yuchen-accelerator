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
        if (result.Count > 0)
            _cache.Set(domain, result);
        return result;
    }

    public void ClearCache() => _cache.Clear();

    private static async Task<List<string>> ResolveWithDnsAsync(string domain, string dnsServer)
    {
        var result = new List<string>();
        try
        {
            using var client = new UdpClient();
            client.Client.ReceiveTimeout = 3000;
            client.Client.SendTimeout = 3000;

            // Build DNS query for A record
            var query = BuildDnsQuery(domain);
            await client.SendAsync(query, query.Length, dnsServer, 53);

            var response = await client.ReceiveAsync();
            var ips = ParseDnsResponse(response.Buffer);
            result.AddRange(ips);
        }
        catch { }

        return result;
    }

    private static byte[] BuildDnsQuery(string domain)
    {
        using var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);

        // Transaction ID (random)
        var rng = new Random();
        writer.Write((ushort)rng.Next(1, 65535));

        // Flags: standard query
        writer.Write((ushort)0x0100);
        // Questions: 1
        writer.Write((ushort)0x0001);
        // Answer RRs
        writer.Write((ushort)0x0000);
        // Authority RRs
        writer.Write((ushort)0x0000);
        // Additional RRs
        writer.Write((ushort)0x0000);

        // Question: encode domain
        foreach (var label in domain.Split('.'))
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(label);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }
        writer.Write((byte)0x00); // End of domain

        // QTYPE: A record (1)
        writer.Write((ushort)0x0001);
        // QCLASS: IN (1)
        writer.Write((ushort)0x0001);

        writer.Flush();
        return ms.ToArray();
    }

    private static List<string> ParseDnsResponse(byte[] response)
    {
        var ips = new List<string>();
        if (response.Length < 12) return ips;

        try
        {
            int answerCount = (response[6] << 8) | response[7];
            int pos = 12;

            // Skip question section
            while (pos < response.Length && response[pos] != 0x00)
            {
                if ((response[pos] & 0xC0) == 0xC0) { pos += 2; break; }
                pos += response[pos] + 1;
            }
            if (response[pos] == 0x00) pos++;
            pos += 4; // Skip QTYPE + QCLASS

            // Read answers
            for (int i = 0; i < answerCount && pos + 10 <= response.Length; i++)
            {
                // Skip name (may be compressed)
                if ((response[pos] & 0xC0) == 0xC0) { pos += 2; }
                else { while (pos < response.Length && response[pos] != 0x00) pos += response[pos] + 1; pos++; }

                if (pos + 10 > response.Length) break;

                int rtype = (response[pos] << 8) | response[pos + 1];
                pos += 2; // Type
                pos += 2; // Class
                pos += 4; // TTL
                int rdLength = (response[pos] << 8) | response[pos + 1];
                pos += 2;

                if (rtype == 1 && rdLength == 4 && pos + 4 <= response.Length) // A record
                {
                    var ip = new IPAddress(response[pos..(pos + 4)]).ToString();
                    ips.Add(ip);
                }

                pos += rdLength;
            }
        }
        catch { }

        return ips;
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
