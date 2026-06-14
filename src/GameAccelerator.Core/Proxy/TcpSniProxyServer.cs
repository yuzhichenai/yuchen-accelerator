using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using GameAccelerator.Core.Dns;
using GameAccelerator.Core.Rules;
using Microsoft.Extensions.Logging;

namespace GameAccelerator.Core.Proxy;

public class TcpSniProxyServer
{
    private readonly RuleEngine _ruleEngine;
    private readonly IDnsOptimizer _dnsOptimizer;
    private readonly ILogger<TcpSniProxyServer> _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _activeConnections;
    private long _totalBytesUp;
    private long _totalBytesDown;

    public bool IsRunning { get; private set; }
    public int Port { get; }
    public int ActiveConnections => _activeConnections;
    public long TotalBytesUp => _totalBytesUp;
    public long TotalBytesDown => _totalBytesDown;

    public TcpSniProxyServer(RuleEngine ruleEngine, IDnsOptimizer dnsOptimizer, ILogger<TcpSniProxyServer> logger, int port = 4433)
    {
        _ruleEngine = ruleEngine;
        _dnsOptimizer = dnsOptimizer;
        _logger = logger;
        Port = port;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, Port);
        _listener.Start();
        IsRunning = true;
        _logger.LogInformation("SNI Proxy started on 127.0.0.1:{Port}", Port);

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                Interlocked.Increment(ref _activeConnections);
                _ = HandleConnectionAsync(client, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsRunning = false;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        IsRunning = false;
        _logger.LogInformation("SNI Proxy stopped");
    }

    public (long up, long down) GetTrafficAndReset()
    {
        return (Interlocked.Exchange(ref _totalBytesUp, 0),
                Interlocked.Exchange(ref _totalBytesDown, 0));
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            var stream = client.GetStream();
            client.ReceiveTimeout = 30000;
            client.SendTimeout = 30000;

            // Peek the first bytes to detect TLS or HTTP
            byte[] peekBuffer = new byte[4096];
            int read = await stream.ReadAsync(peekBuffer, 0, peekBuffer.Length, ct);
            if (read == 0) return;

            string? domain = null;
            byte[] originalData = peekBuffer[..read];

            if (read >= 5 && peekBuffer[0] == 0x16) // TLS ClientHello
            {
                domain = ParseSni(originalData);
            }
            else // HTTP
            {
                domain = ParseHttpHost(originalData);
            }

            if (string.IsNullOrEmpty(domain))
            {
                _logger.LogDebug("Could not parse domain, direct forwarding");
                return;
            }

            _logger.LogDebug("Parsed domain: {Domain}", domain);

            // Find matching rule
            var rule = _ruleEngine.Match(domain);
            string? upstreamIp = null;
            int upstreamPort = 443;

            if (rule != null)
            {
                upstreamPort = rule.UpstreamPort;
                _logger.LogDebug("Matched rule: {Pattern}, strategy: {Strategy}, type: {UpstreamType}",
                    rule.DomainPattern, rule.ProxyStrategy, rule.UpstreamType);

                upstreamIp = rule.UpstreamType switch
                {
                    UpstreamType.StaticIp => rule.UpstreamList.FirstOrDefault(),
                    UpstreamType.FastestCdn => await _dnsOptimizer.GetFastestIpAsync(domain),
                    UpstreamType.DnsOnly => (await _dnsOptimizer.ResolveAsync(domain)).FirstOrDefault(),
                    _ => null
                };
            }

            if (string.IsNullOrEmpty(upstreamIp))
            {
                // Fallback to standard DNS
                try
                {
                    var addresses = await System.Net.Dns.GetHostAddressesAsync(domain, ct);
                    upstreamIp = addresses.FirstOrDefault()?.ToString();
                }
                catch { }
            }

            if (string.IsNullOrEmpty(upstreamIp))
            {
                _logger.LogWarning("No upstream IP for {Domain}", domain);
                return;
            }

            _logger.LogDebug("Connecting to upstream {Ip}:{Port}", upstreamIp, upstreamPort);

            using var upstream = new TcpClient();
            await upstream.ConnectAsync(IPAddress.Parse(upstreamIp), upstreamPort, ct);

            var upstreamStream = upstream.GetStream();

            // Send the originally read data to upstream
            await upstreamStream.WriteAsync(originalData, 0, originalData.Length, ct);

            // Bidirectional relay
            var relayTask1 = RelayAsync(stream, upstreamStream, ct, true);
            var relayTask2 = RelayAsync(upstreamStream, stream, ct, false);
            await Task.WhenAny(relayTask1, relayTask2);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connection handling error");
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
            client.Dispose();
        }
    }

    private async Task RelayAsync(NetworkStream from, NetworkStream to, CancellationToken ct, bool isUpload)
    {
        try
        {
            var buffer = new byte[8192];
            while (!ct.IsCancellationRequested)
            {
                int read = await from.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read == 0) break;
                await to.WriteAsync(buffer, 0, read, ct);

                if (isUpload)
                    Interlocked.Add(ref _totalBytesUp, read);
                else
                    Interlocked.Add(ref _totalBytesDown, read);
            }
        }
        catch { }
    }

    private static string? ParseSni(byte[] data)
    {
        try
        {
            int pos = 0;

            // TLS Record: ContentType(1) + Version(2) + Length(2) = 5 bytes header
            if (data.Length < 5) return null;
            pos += 5; // Skip record header

            // Handshake: HandshakeType(1) + Length(3)
            if (pos + 4 > data.Length) return null;
            pos += 1; // Skip handshake type
            int handshakeLen = (data[pos] << 16) | (data[pos + 1] << 8) | data[pos + 2];
            pos += 3;

            // ClientVersion(2) + Random(32)
            pos += 34;

            // Session ID (1 byte length + data)
            if (pos >= data.Length) return null;
            int sessionIdLen = data[pos++];
            pos += sessionIdLen;

            // Cipher Suites (2 bytes length + data)
            if (pos + 2 > data.Length) return null;
            int cipherLen = (data[pos] << 8) | data[pos + 1];
            pos += 2 + cipherLen;

            // Compression Methods (1 byte length + data)
            if (pos >= data.Length) return null;
            int compLen = data[pos++];
            pos += compLen;

            // Extensions (2 bytes total length)
            if (pos + 2 > data.Length) return null;
            int extTotalLen = (data[pos] << 8) | data[pos + 1];
            pos += 2;
            int extEnd = pos + extTotalLen;

            while (pos + 4 <= data.Length && pos < extEnd)
            {
                int extType = (data[pos] << 8) | data[pos + 1];
                int extLen = (data[pos + 2] << 8) | data[pos + 3];
                pos += 4;

                if (extType == 0x0000) // SNI
                {
                    if (pos + 5 > data.Length) return null;
                    // Server Name List length(2) + ServerNameType(1) + ServerNameLength(2)
                    int snListLen = (data[pos] << 8) | data[pos + 1];
                    pos += 2;
                    int snEnd = pos + snListLen;

                    while (pos + 3 <= data.Length && pos < snEnd)
                    {
                        int snType = data[pos++];
                        int snLen = (data[pos] << 8) | data[pos + 1];
                        pos += 2;

                        if (snType == 0 && pos + snLen <= data.Length) // hostname
                        {
                            return System.Text.Encoding.ASCII.GetString(data, pos, snLen);
                        }
                        pos += snLen;
                    }
                    break;
                }
                pos += extLen;
            }
        }
        catch { }
        return null;
    }

    private static string? ParseHttpHost(byte[] data)
    {
        try
        {
            var text = System.Text.Encoding.ASCII.GetString(data);
            var lines = text.Split("\r\n");
            foreach (var line in lines)
            {
                if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    var host = line[5..].Trim();
                    // Strip port if present
                    var portIdx = host.LastIndexOf(':');
                    if (portIdx > 0 && int.TryParse(host[(portIdx + 1)..], out _))
                        host = host[..portIdx];
                    return host;
                }
            }
        }
        catch { }
        return null;
    }
}
