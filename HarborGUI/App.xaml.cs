using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using HarborGUI.Services;
using HarborGUI.Services.Interfaces;
using HarborGUI.ViewModels;

namespace HarborGUI;

/// <summary>
/// App 入口：配置依赖注入容器并启动主窗口
/// </summary>
public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // 日志服务（单例，全局共享）
        services.AddSingleton<ILogService, LogService>();

        // 注册服务（单例，全局共享）
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ITaskDiscoveryService, TaskDiscoveryService>();
        services.AddSingleton<IProcessRunnerService, ProcessRunnerService>();
        services.AddSingleton<IReportService, ReportService>();

        // VariableResolver：单例，从 ConfigService 加载初始值
        services.AddSingleton<IVariableResolver>(sp =>
        {
            var configService = sp.GetRequiredService<IConfigService>();
            var appConfig = configService.LoadAppConfig();
            return new VariableResolver(appConfig.EnvironmentVariables);
        });

        // 瞬时服务（每次请求新建）
        services.AddTransient<ITaskExtractionService, TaskExtractionService>();
        services.AddTransient<IVerifyService, VerifyService>();

        // 注册 ViewModel 和 Window
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}

