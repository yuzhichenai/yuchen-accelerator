namespace GameAccelerator.Core.Configuration;

public class AppConfig
{
    public int HttpProxyPort { get; set; } = 8080;
    public int SniProxyPort { get; set; } = 4433;
    public int Socks5Port { get; set; } = 10808;
    public bool EnableSteamAcceleration { get; set; } = true;
    public bool EnableGitHubAcceleration { get; set; } = true;
    public bool EnableGameAcceleration { get; set; } = true;
    public bool EnableSystemProxy { get; set; } = true;
    public bool EnableTcpOptimization { get; set; } = true;
    public bool AutoStart { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public List<string> CustomHostsEntries { get; set; } = new();
}
