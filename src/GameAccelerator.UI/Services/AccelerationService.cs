using System.ComponentModel;
using System.Runtime.CompilerServices;
using GameAccelerator.Core.Configuration;
using GameAccelerator.Core.Dns;
using GameAccelerator.Core.Hosts;
using GameAccelerator.Core.Network;
using GameAccelerator.Core.Proxy;
using GameAccelerator.Core.Rules;
using GameAccelerator.Core.Socks5;
using Microsoft.Extensions.Logging;

namespace GameAccelerator.UI.Services;

public class AccelerationService
{
    private readonly TcpSniProxyServer _sniProxy;
    private readonly Socks5ProxyServer _socks5;
    private readonly SystemProxyManager _proxyManager;
    private readonly HostsManager _hostsManager;
    private readonly IDnsOptimizer _dnsOptimizer;
    private readonly RuleEngine _ruleEngine;
    private readonly TcpOptimizer _tcpOptimizer;
    private readonly AppConfig _config;
    private readonly ILogger<AccelerationService> _logger;

    private CancellationTokenSource? _cts;
    private Task? _sniTask;
    private Task? _socks5Task;
    private bool _isRunning;

    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(IsStopped)); }
    }

    public string StatusText => IsRunning ? "加速中" : "已停止";
    public bool IsStopped => !IsRunning;
    public event PropertyChangedEventHandler? PropertyChanged;

    public AccelerationService(
        TcpSniProxyServer sniProxy,
        Socks5ProxyServer socks5,
        SystemProxyManager proxyManager,
        HostsManager hostsManager,
        IDnsOptimizer dnsOptimizer,
        RuleEngine ruleEngine,
        TcpOptimizer tcpOptimizer,
        AppConfig config,
        ILogger<AccelerationService> logger)
    {
        _sniProxy = sniProxy;
        _socks5 = socks5;
        _proxyManager = proxyManager;
        _hostsManager = hostsManager;
        _dnsOptimizer = dnsOptimizer;
        _ruleEngine = ruleEngine;
        _tcpOptimizer = tcpOptimizer;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> StartAsync()
    {
        if (IsRunning) return true;

        try
        {
            _cts = new CancellationTokenSource();
            _logger.LogInformation("Starting acceleration services...");

            // 1. Start SNI proxy
            _sniTask = _sniProxy.StartAsync();

            // 2. Start SOCKS5 proxy
            _socks5Task = _socks5.StartAsync();

            // 3. Apply hosts entries
            if (_config.EnableSteamAcceleration || _config.EnableGitHubAcceleration)
            {
                await ApplyHostsEntriesAsync();
            }

            // 4. Set system proxy
            if (_config.EnableSystemProxy)
            {
                var proxyAddr = $"127.0.0.1:{_config.SniProxyPort}";
                _proxyManager.EnableProxy(proxyAddr);
            }

            // 5. TCP optimization
            if (_config.EnableTcpOptimization)
            {
                _tcpOptimizer.Apply();
            }

            IsRunning = true;
            _logger.LogInformation("Acceleration started successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start acceleration");
            await StopAsync();
            return false;
        }
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping acceleration services...");

        // 1. Stop SNI proxy
        _sniProxy.Stop();

        // 2. Stop SOCKS5
        _socks5.Stop();

        // 3. Remove hosts entries
        await _hostsManager.RemoveEntriesAsync();

        // 4. Restore system proxy
        _proxyManager.DisableProxy();

        // 5. Cancel token
        _cts?.Cancel();

        IsRunning = false;
        _logger.LogInformation("Acceleration stopped");
    }

    private async Task ApplyHostsEntriesAsync()
    {
        var entries = new List<HostsEntry>();
        var hostsRules = _ruleEngine.GetHostsRedirectRules();

        foreach (var rule in hostsRules)
        {
            var domain = rule.DomainPattern.TrimStart('*', '.');
            if (_config.EnableSteamAcceleration &&
                (domain.Contains("steam") || domain.Contains("steampowered")))
            {
                entries.Add(new HostsEntry
                {
                    IpAddress = "127.0.0.1",
                    Hostname = domain,
                    Comment = $"GameAccelerator - {rule.Description}"
                });
            }

            if (_config.EnableGitHubAcceleration && domain.Contains("github"))
            {
                entries.Add(new HostsEntry
                {
                    IpAddress = "127.0.0.1",
                    Hostname = domain,
                    Comment = $"GameAccelerator - {rule.Description}"
                });
            }
        }

        if (entries.Count > 0)
            await _hostsManager.ApplyEntriesAsync(entries);
    }

    public (long up, long down) GetProxyTraffic() => _sniProxy.GetTrafficAndReset();

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
