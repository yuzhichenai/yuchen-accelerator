using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GameAccelerator.Core.Network;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;

namespace GameAccelerator.UI.ViewModels;

public partial class TrafficViewModel : BaseViewModel
{
    private readonly TrafficCounter _trafficCounter;
    private const int MaxDataPoints = 60;

    [ObservableProperty]
    private ObservableCollection<ObservablePoint> _downloadSeries = new();

    [ObservableProperty]
    private ObservableCollection<ObservablePoint> _uploadSeries = new();

    [ObservableProperty]
    private string _totalDownloaded = "0 B";

    [ObservableProperty]
    private string _totalUploaded = "0 B";

    [ObservableProperty]
    private string _peakDownload = "0 B/s";

    [ObservableProperty]
    private string _peakUpload = "0 B/s";

    public ISeries[] Series { get; set; }
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    public TrafficViewModel(TrafficCounter trafficCounter)
    {
        _trafficCounter = trafficCounter;

        for (int i = 0; i < MaxDataPoints; i++)
        {
            DownloadSeries.Add(new ObservablePoint(i, 0));
            UploadSeries.Add(new ObservablePoint(i, 0));
        }

        Series = new ISeries[]
        {
            new LineSeries<ObservablePoint>
            {
                Values = DownloadSeries,
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColors.DodgerBlue) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3
            },
            new LineSeries<ObservablePoint>
            {
                Values = UploadSeries,
                Stroke = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(SkiaSharp.SKColors.Orange) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0,
                LineSmoothness = 0.3
            }
        };

        XAxes = new Axis[]
        {
            new Axis { ShowSeparatorLines = false, IsVisible = false }
        };
        YAxes = new Axis[]
        {
            new Axis { ShowSeparatorLines = true, IsVisible = true }
        };
    }

    private long _peakDownValue;
    private long _peakUpValue;

    public void UpdateData()
    {
        var (upSpeed, downSpeed) = _trafficCounter.GetSpeed();
        var (totalUp, totalDown) = _trafficCounter.GetTotals();

        TotalDownloaded = FormatBytes(totalDown);
        TotalUploaded = FormatBytes(totalUp);

        if (downSpeed > _peakDownValue)
        {
            _peakDownValue = downSpeed;
            PeakDownload = FormatSpeed(downSpeed);
        }
        if (upSpeed > _peakUpValue)
        {
            _peakUpValue = upSpeed;
            PeakUpload = FormatSpeed(upSpeed);
        }

        // Shift all points left
        for (int i = 0; i < MaxDataPoints - 1; i++)
        {
            DownloadSeries[i].Y = DownloadSeries[i + 1].Y;
            UploadSeries[i].Y = UploadSeries[i + 1].Y;
        }

        // Add new point at the end
        DownloadSeries[MaxDataPoints - 1].Y = downSpeed;
        UploadSeries[MaxDataPoints - 1].Y = upSpeed;
    }

    private static string FormatSpeed(long bytesPerSec)
    {
        if (bytesPerSec < 1024) return $"{bytesPerSec} B/s";
        if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024.0:F1} KB/s";
        return $"{bytesPerSec / (1024.0 * 1024):F1} MB/s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
