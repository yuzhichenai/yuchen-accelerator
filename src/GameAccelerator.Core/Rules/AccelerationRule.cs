namespace GameAccelerator.Core.Rules;

public enum ProxyStrategy
{
    SniProxy,
    HostsRedirect,
    Direct
}

public enum UpstreamType
{
    StaticIp,
    FastestCdn,
    DnsOnly
}

public class AccelerationRule
{
    public string DomainPattern { get; set; } = "";
    public ProxyStrategy ProxyStrategy { get; set; } = ProxyStrategy.SniProxy;
    public UpstreamType UpstreamType { get; set; } = UpstreamType.FastestCdn;
    public List<string> UpstreamList { get; set; } = new();
    public int UpstreamPort { get; set; } = 443;
    public int Priority { get; set; } = 100;
    public string Description { get; set; } = "";
    public bool IsHostsRedirect => ProxyStrategy == ProxyStrategy.HostsRedirect;
}
