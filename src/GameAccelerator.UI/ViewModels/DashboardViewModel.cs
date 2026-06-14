using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameAccelerator.Core.Network;
using GameAccelerator.UI.Services;

namespace GameAccelerator.UI.ViewModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly AccelerationService _accelService;
    private readonly TrafficCounter _trafficCounter;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "已停止";

    [ObservableProperty]
    private string _downloadSpeed = "0 B/s";

    [ObservableProperty]
    private string _uploadSpeed = "0 B/s";

    [ObservableProperty]
    private string _totalDownloaded = "0 B";

    [ObservableProperty]
    private string _totalUploaded = "0 B";

    [ObservableProperty]
    private int _activeConnections;

    [ObservableProperty]
    private bool _steamEnabled = true;

    [ObservableProperty]
    private bool _githubEnabled = true;

    [ObservableProperty]
    private bool _gamesEnabled = true;

    public DashboardViewModel(AccelerationService accelService, TrafficCounter trafficCounter)
    {
        _accelService = accelService;
        _trafficCounter = trafficCounter;
    }

    [RelayCommand]
    private async Task ToggleAcceleration()
    {
        if (_accelService.IsRunning)
        {
            await _accelService.StopAsync();
        }
        else
        {
            await _accelService.StartAsync();
        }
        IsRunning = _accelService.IsRunning;
        StatusText = _accelService.StatusText;
    }

    public void UpdateTrafficStats()
    {
        var (upSpeed, downSpeed) = _trafficCounter.GetSpeed();
        var (totalUp, totalDown) = _trafficCounter.GetTotals();

        DownloadSpeed = FormatSpeed(downSpeed);
        UploadSpeed = FormatSpeed(upSpeed);
        TotalDownloaded = FormatBytes(totalDown);
        TotalUploaded = FormatBytes(totalUp);
    }

    private static string FormatSpeed(long bytesPerSec)
    {
        if (bytesPerSec < 1024) return $"{bytesPerSec} B/s";
        if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024.0:F1} KB/s";
        if (bytesPerSec < 1024 * 1024 * 1024) return $"{bytesPerSec / (1024.0 * 1024):F1} MB/s";
        return $"{bytesPerSec / (1024.0 * 1024 * 1024):F2} GB/s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
