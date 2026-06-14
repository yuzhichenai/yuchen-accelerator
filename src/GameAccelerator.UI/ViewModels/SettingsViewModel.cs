using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameAccelerator.Core.Configuration;

namespace GameAccelerator.UI.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly ConfigManager _configManager;
    private readonly AppConfig _config;

    [ObservableProperty]
    private int _sniProxyPort;

    [ObservableProperty]
    private int _socks5Port;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private string _statusMessage = "";

    public SettingsViewModel(ConfigManager configManager, AppConfig config)
    {
        _configManager = configManager;
        _config = config;
        _sniProxyPort = config.SniProxyPort;
        _socks5Port = config.Socks5Port;
        _autoStart = config.AutoStart;
        _minimizeToTray = config.MinimizeToTray;
    }

    partial void OnSniProxyPortChanged(int value) => _config.SniProxyPort = value;
    partial void OnSocks5PortChanged(int value) => _config.Socks5Port = value;
    partial void OnAutoStartChanged(bool value) => _config.AutoStart = value;
    partial void OnMinimizeToTrayChanged(bool value) => _config.MinimizeToTray = value;

    [RelayCommand]
    private void SaveSettings()
    {
        _configManager.Save(_config);
        StatusMessage = "设置已保存";
    }
}
