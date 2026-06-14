using System.Windows;
using GameAccelerator.Core.Configuration;
using GameAccelerator.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameAccelerator.UI;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core services
        services.AddGameAcceleratorCore();

        // UI services
        services.AddSingleton<AccelerationService>();

        // ViewModels
        services.AddTransient<ViewModels.DashboardViewModel>();
        services.AddTransient<ViewModels.ServiceListViewModel>();
        services.AddTransient<ViewModels.TrafficViewModel>();
        services.AddTransient<ViewModels.SettingsViewModel>();

        // MainWindow
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load rules (embedded resources)
        var ruleEngine = _serviceProvider.GetRequiredService<Core.Rules.RuleEngine>();
        ruleEngine.Reload();

        // Single instance check
        var mutex = new Mutex(true, "GameAccelerator_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("游戏加速器已在运行中。", "Game Accelerator",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Handle minimize to tray config
        var config = _serviceProvider.GetRequiredService<AppConfig>();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        if (!config.MinimizeToTray)
        {
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Show();
        }
        else
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var accelService = _serviceProvider.GetRequiredService<AccelerationService>();
        if (accelService.IsRunning)
            accelService.StopAsync().Wait();

        _serviceProvider.Dispose();
        base.OnExit(e);
    }

    public T GetService<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
}
