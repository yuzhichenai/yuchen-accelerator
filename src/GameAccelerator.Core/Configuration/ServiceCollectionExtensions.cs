using GameAccelerator.Core.Dns;
using GameAccelerator.Core.Hosts;
using GameAccelerator.Core.Network;
using GameAccelerator.Core.Proxy;
using GameAccelerator.Core.Rules;
using GameAccelerator.Core.Socks5;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameAccelerator.Core.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameAcceleratorCore(this IServiceCollection services)
    {
        // Configuration
        services.AddSingleton<ConfigManager>();
        services.AddSingleton(sp => sp.GetRequiredService<ConfigManager>().Load());

        // Rules
        services.AddSingleton<EmbeddedRuleProvider>();
        services.AddSingleton<IRuleProvider>(sp => sp.GetRequiredService<EmbeddedRuleProvider>());
        services.AddSingleton<RuleEngine>(sp =>
        {
            var provider = sp.GetRequiredService<EmbeddedRuleProvider>();
            var engine = new RuleEngine(provider);
            engine.Reload();
            return engine;
        });

        // DNS
        services.AddSingleton<IDnsOptimizer, DnsOptimizer>();

        // Hosts
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<AppConfig>();
            var cm = sp.GetRequiredService<ConfigManager>();
            return new HostsManager(cm.GetAppDataPath());
        });

        // Proxy
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<AppConfig>();
            var engine = sp.GetRequiredService<RuleEngine>();
            var dns = sp.GetRequiredService<IDnsOptimizer>();
            var logger = sp.GetRequiredService<ILogger<TcpSniProxyServer>>();
            return new TcpSniProxyServer(engine, dns, logger, config.SniProxyPort);
        });

        // SOCKS5
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<AppConfig>();
            var logger = sp.GetRequiredService<ILogger<Socks5ProxyServer>>();
            return new Socks5ProxyServer(logger, config.Socks5Port);
        });

        // Network
        services.AddSingleton<SystemProxyManager>();
        services.AddSingleton<TcpOptimizer>();
        services.AddSingleton<TrafficCounter>();

        return services;
    }
}
