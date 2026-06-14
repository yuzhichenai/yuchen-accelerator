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
    private DispatcherTimer? _trafficTimer;
    private TaskbarIcon? _trayIcon;

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

        // Create system tray icon
        SetupTrayIcon();

        // Traffic update timer
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
        _trayIcon = new TaskbarIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            ToolTipText = "游戏加速器 - 已停止",
            Visibility = Visibility.Visible
        };

        _trayIcon.TrayLeftMouseDown += (s, e) =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        };

        _trayIcon.ContextMenu = new System.Windows.Controls.ContextMenu();
        var showItem = new MenuItem { Header = "显示窗口" };
        showItem.Click += (s, e) => { Show(); WindowState = WindowState.Normal; Activate(); };

        var toggleItem = new MenuItem();
        toggleItem.Click += async (s, e) =>
        {
            if (_accelService.IsRunning)
                await _accelService.StopAsync();
            else
                await _accelService.StartAsync();
            UpdateStatus();
        };

        var exitItem = new MenuItem { Header = "退出" };
        exitItem.Click += (s, e) => Application.Current.Shutdown();

        _trayIcon.ContextMenu.Items.Add(showItem);
        _trayIcon.ContextMenu.Items.Add(new Separator());
        _trayIcon.ContextMenu.Items.Add(toggleItem);
        _trayIcon.ContextMenu.Items.Add(new Separator());
        _trayIcon.ContextMenu.Items.Add(exitItem);
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

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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
        }
    }
}
