using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace GameAccelerator.Core.Socks5;

public class Socks5ProxyServer
{
    private readonly ILogger<Socks5ProxyServer> _logger;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private int _activeConnections;

    public bool IsRunning { get; private set; }
    public int Port { get; }

    public Socks5ProxyServer(ILogger<Socks5ProxyServer> logger, int port = 10808)
    {
        _logger = logger;
        Port = port;
    }

    public async Task StartAsync()
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, Port);
        _listener.Start();
        IsRunning = true;
        _logger.LogInformation("SOCKS5 Proxy started on 127.0.0.1:{Port}", Port);

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
    }

    private async Task HandleConnectionAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            var stream = client.GetStream();
            client.ReceiveTimeout = 10000;
            client.SendTimeout = 10000;

            // [1] Greeting: client sends ver(1) + nmethods(1) + methods(nmethods)
            byte[] greet = new byte[256];
            int read = await stream.ReadAsync(greet, 0, 2, ct);
            if (read < 2) return;

            int nmethods = greet[1];
            await stream.ReadExactlyAsync(greet, 0, nmethods, ct);

            // Respond: no auth (0x00)
            await stream.WriteAsync(new byte[] { 0x05, 0x00 }, ct);

            // [2] Request
            byte[] request = new byte[1024];
            int reqRead = await stream.ReadAsync(request, 0, 4, ct);
            if (reqRead < 4) return;

            int cmd = request[1];
            int atyp = request[3];

            string dstAddr;
            int dstPort;

            if (atyp == 0x01) // IPv4
            {
                await stream.ReadExactlyAsync(request, 0, 4, ct);
                dstAddr = new IPAddress(request[..4]).ToString();
            }
            else if (atyp == 0x03) // Domain name
            {
                await stream.ReadExactlyAsync(request, 0, 1, ct);
                int nameLen = request[0];
                await stream.ReadExactlyAsync(request, 0, nameLen, ct);
                dstAddr = System.Text.Encoding.ASCII.GetString(request, 0, nameLen);
            }
            else if (atyp == 0x04) // IPv6
            {
                await stream.ReadExactlyAsync(request, 0, 16, ct);
                dstAddr = new IPAddress(request[..16]).ToString();
            }
            else
            {
                await SendReplyAsync(stream, 0x08); // address type not supported
                return;
            }

            // Read port (2 bytes)
            await stream.ReadExactlyAsync(request, 0, 2, ct);
            dstPort = (request[0] << 8) | request[1];

            _logger.LogDebug("SOCKS5 request: CMD={Cmd} ADDR={Addr}:{Port}", cmd, dstAddr, dstPort);

            if (cmd == 0x01) // CONNECT
            {
                await HandleConnectAsync(stream, dstAddr, dstPort, ct);
            }
            else
            {
                await SendReplyAsync(stream, 0x07); // command not supported
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SOCKS5 connection error");
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);
            client.Dispose();
        }
    }

    private async Task HandleConnectAsync(NetworkStream clientStream, string host, int port, CancellationToken ct)
    {
        try
        {
            using var target = new TcpClient();
            await target.ConnectAsync(host, port, ct);

            var bindAddr = ((IPEndPoint)target.Client.LocalEndPoint!).Address;
            int bindPort = ((IPEndPoint)target.Client.LocalEndPoint!).Port;

            await SendReplyAsync(clientStream, 0x00, bindAddr, bindPort);

            var targetStream = target.GetStream();
            var t1 = clientStream.CopyToAsync(targetStream, ct);
            var t2 = targetStream.CopyToAsync(clientStream, ct);
            await Task.WhenAny(t1, t2);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SOCKS5 CONNECT error: {Host}:{Port}", host, port);
            await SendReplyAsync(clientStream, 0x01); // general failure
        }
    }

    private static async Task SendReplyAsync(NetworkStream stream, byte rep, IPAddress? bindAddr = null, int bindPort = 0)
    {
        bindAddr ??= IPAddress.Loopback;
        var reply = new byte[10];
        reply[0] = 0x05; // ver
        reply[1] = rep;  // rep
        reply[2] = 0x00; // rsv
        reply[3] = 0x01; // atyp (IPv4)

        var addrBytes = bindAddr.GetAddressBytes();
        Array.Copy(addrBytes, 0, reply, 4, 4);
        reply[8] = (byte)(bindPort >> 8);
        reply[9] = (byte)(bindPort & 0xFF);

        await stream.WriteAsync(reply);
    }
}
