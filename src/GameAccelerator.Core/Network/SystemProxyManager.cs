using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GameAccelerator.Core.Network;

public class SystemProxyManager
{
    private const string InternetSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private readonly ILogger<SystemProxyManager> _logger;
    private bool _proxyWasEnabled;
    private string? _previousServer;

    public SystemProxyManager(ILogger<SystemProxyManager> logger)
    {
        _logger = logger;
    }

    public bool EnableProxy(string proxyAddress)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(InternetSettingsKey, true);
            if (key == null) return false;

            // Save previous state
            var prevValue = key.GetValue("ProxyEnable");
            _proxyWasEnabled = prevValue is int i && i == 1;
            _previousServer = key.GetValue("ProxyServer") as string;

            key.SetValue("ProxyEnable", 1);
            key.SetValue("ProxyServer", proxyAddress);

            RefreshInternetOptions();
            _logger.LogInformation("System proxy enabled: {Proxy}", proxyAddress);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable system proxy");
            return false;
        }
    }

    public bool DisableProxy()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(InternetSettingsKey, true);
            if (key == null) return false;

            key.SetValue("ProxyEnable", _proxyWasEnabled ? 1 : 0);
            if (_previousServer != null)
                key.SetValue("ProxyServer", _previousServer);

            RefreshInternetOptions();
            _logger.LogInformation("System proxy disabled");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable system proxy");
            return false;
        }
    }

    private static void RefreshInternetOptions()
    {
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
    }

    private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
    private const int INTERNET_OPTION_REFRESH = 37;

    [DllImport("wininet.dll")]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    public void SetWinHttpProxy(string proxyAddress)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"winhttp set proxy proxy-server=\"{proxyAddress}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            _logger.LogInformation("WinHTTP proxy set: {Proxy}", proxyAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set WinHTTP proxy");
        }
    }

    public void RemoveWinHttpProxy()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "winhttp reset proxy",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            _logger.LogInformation("WinHTTP proxy removed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove WinHTTP proxy");
        }
    }
}
