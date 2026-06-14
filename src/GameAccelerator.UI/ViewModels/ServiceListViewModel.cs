using CommunityToolkit.Mvvm.ComponentModel;
using GameAccelerator.Core.Configuration;

namespace GameAccelerator.UI.ViewModels;

public partial class ServiceListViewModel : BaseViewModel
{
    private readonly AppConfig _config;

    [ObservableProperty]
    private bool _steamEnabled;

    [ObservableProperty]
    private bool _githubEnabled;

    [ObservableProperty]
    private bool _gamesEnabled;

    [ObservableProperty]
    private bool _systemProxyEnabled;

    [ObservableProperty]
    private bool _tcpOptimizationEnabled;

    public ServiceListViewModel(AppConfig config)
    {
        _config = config;
        _steamEnabled = config.EnableSteamAcceleration;
        _githubEnabled = config.EnableGitHubAcceleration;
        _gamesEnabled = config.EnableGameAcceleration;
        _systemProxyEnabled = config.EnableSystemProxy;
        _tcpOptimizationEnabled = config.EnableTcpOptimization;
    }

    partial void OnSteamEnabledChanged(bool value) => _config.EnableSteamAcceleration = value;
    partial void OnGithubEnabledChanged(bool value) => _config.EnableGitHubAcceleration = value;
    partial void OnGamesEnabledChanged(bool value) => _config.EnableGameAcceleration = value;
    partial void OnSystemProxyEnabledChanged(bool value) => _config.EnableSystemProxy = value;
    partial void OnTcpOptimizationEnabledChanged(bool value) => _config.EnableTcpOptimization = value;
}
