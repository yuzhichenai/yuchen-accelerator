using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GameAccelerator.UI.Services;
using GameAccelerator.UI.ViewModels;
using H.NotifyIcon;

namespace GameAccelerator.UI;

public partial class MainWindow : Window
{
    private readonly AccelerationService _accelService;
    private readonly DashboardViewModel _dashboardVM;
    private readonly TrafficViewModel _trafficVM;
    private bool _hasShownTrayTip;
    private DispatcherTimer? _trafficTimer;
    private TaskbarIcon? _trayIcon;
    private MenuItem? _trayToggleItem;

    public MainWindow(
        AccelerationService accelService,
        DashboardViewModel dashboardVM,
        ServiceListViewModel serviceListVM,
        TrafficViewModel trafficVM,
        SettingsViewModel settingsVM)
    {
        InitializeComponent();
        _accelService = accelService;
        _dashboardVM = dashboardVM;
        _trafficVM = trafficVM;

        DataContext = dashboardVM;
        NavListBox.SelectedIndex = 0;

        _accelService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AccelerationService.IsRunning))
                Dispatcher.Invoke(() => UpdateStatus());
        };

        // Defer tray icon creation until window handle is ready
        Loaded += (s, e) => SetupTrayIcon();

        _trafficTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _trafficTimer.Tick += (s, e) =>
        {
            if (_accelService.IsRunning)
            {
                _dashboardVM.UpdateTrafficStats();
                _trafficVM.UpdateData();
            }
        };
        _trafficTimer.Start();
    }

    private void SetupTrayIcon()
    {
        try
        {
            _trayIcon = new TaskbarIcon
            {
                Icon = CreateTrayIcon(),
                ToolTipText = "游戏加速器 - 已停止",
                Visibility = Visibility.Visible
            };

            _trayIcon.TrayLeftMouseDown += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };

            var menu = new ContextMenu();

            var showItem = new MenuItem { Header = "显示窗口" };
            showItem.Click += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };

            _trayToggleItem = new MenuItem { Header = "开启加速" };
            _trayToggleItem.Click += async (s, e) =>
            {
                if (_accelService.IsRunning)
                    await _accelService.StopAsync();
                else
                    await _accelService.StartAsync();
                UpdateStatus();
            };

            var exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += (s, e) => Application.Current.Shutdown();

            menu.Items.Add(showItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(_trayToggleItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            _trayIcon.ContextMenu = menu;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray icon creation failed: {ex.Message}");
        }
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        var bmp = new System.Drawing.Bitmap(32, 32);
        using var g = System.Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 120, 212));
        g.FillEllipse(brush, 2, 2, 28, 28);
        using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2.5f);
        // Lightning bolt
        g.DrawLine(pen, 18, 4, 12, 16);
        g.DrawLine(pen, 12, 16, 20, 16);
        g.DrawLine(pen, 20, 16, 10, 28);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private void UpdateStatus()
    {
        _dashboardVM.IsRunning = _accelService.IsRunning;
        _dashboardVM.StatusText = _accelService.StatusText;
        UpdateStatusDot();

        if (_trayIcon != null)
            _trayIcon.ToolTipText = _accelService.IsRunning
                ? "游戏加速器 - 加速中"
                : "游戏加速器 - 已停止";

        if (_trayToggleItem != null)
            _trayToggleItem.Header = _accelService.IsRunning ? "停止加速" : "开启加速";
    }

    private void UpdateStatusDot()
    {
        if (_accelService.IsRunning)
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10));
        else
            StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38));
    }

    private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is not ListBoxItem item) return;

        var tag = item.Tag?.ToString();
        DashboardPanel.Visibility = tag == "Dashboard" ? Visibility.Visible : Visibility.Collapsed;
        ServiceListPanel.Visibility = tag == "Services" ? Visibility.Visible : Visibility.Collapsed;
        TrafficPanel.Visibility = tag == "Traffic" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = tag == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "Services")
            DataContext = ((App)Application.Current).GetService<ServiceListViewModel>();
        else if (tag == "Traffic")
            DataContext = _trafficVM;
        else if (tag == "Settings")
            DataContext = ((App)Application.Current).GetService<SettingsViewModel>();
        else
            DataContext = _dashboardVM;
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        var config = ((App)Application.Current).GetService<Core.Configuration.AppConfig>();
        if (config.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            _trayIcon?.Dispose();
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        var config = ((App)Application.Current).GetService<Core.Configuration.AppConfig>();
        if (WindowState == WindowState.Minimized && config.MinimizeToTray)
        {
            Hide();
            if (!_hasShownTrayTip && _trayIcon != null)
            {
                _trayIcon.ShowNotification("已最小化到系统托盘", "双击托盘图标即可恢复窗口");
                _hasShownTrayTip = true;
            }
        }
    }
}
