using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Channels;

// 引用契约、驱动、服务及基础设施
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Services;
using DHDAS.Contracts.Drivers;
using DHDAS.Service.Signal;
using DHDAS.App.Shell.ViewModels;
using DHDAS.App.Shell.Services;
using DHDAS.Driver.Simulator;
using DHDAS.Infrastructure.Core.Session;
using DHDAS.Service.Signal.Network;

namespace DHDAS.App.Shell;

class Program
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // 1. 初始化 .NET Generic Host (后台引擎)
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // --- A. 基础设施注册 ---
                services.AddSingleton<SessionManager>();
                services.AddSingleton<IDeviceDriver, SineWaveSimulator>();
                services.AddSingleton<PluginManager>();
                services.AddSingleton<MainWindowViewModel>();

                // --- B. 管道编排器注册 ---
                services.AddSingleton<PipelineOrchestrator>();

                // --- C. 业务节点注册 (关键：双重身份绑定) ---

                // 注册发送节点
                services.AddSingleton<NetworkSenderNode>();
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<NetworkSenderNode>());
                services.AddHostedService(sp => sp.GetRequiredService<NetworkSenderNode>());

                // 注册接收节点
                services.AddSingleton<NetworkReceiverNode>();
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<NetworkReceiverNode>());
                services.AddHostedService(sp => sp.GetRequiredService<NetworkReceiverNode>());

                services.AddSingleton<DHDAS.Application.Support.IMessenger, DHDAS.Application.Support.AppMessenger>();

                // 注册数据采集节点
                services.AddSingleton<AcquisitionService>();
                // 身份1：作为管道节点
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<AcquisitionService>());
                // 身份2：作为后台托管服务
                services.AddHostedService(sp => sp.GetRequiredService<AcquisitionService>());

                // 注册数据推送节点
                services.AddSingleton<DataPushService>();
                // 身份1：作为业务接口供应用层调用
                services.AddSingleton<IDataPushService>(sp => sp.GetRequiredService<DataPushService>());
                // 身份2：作为管道节点
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<DataPushService>());
                // 身份3：作为后台托管服务
                services.AddHostedService(sp => sp.GetRequiredService<DataPushService>());

                // 注册存储节点
                services.AddSingleton<StorageService>();
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<StorageService>());
                services.AddHostedService(sp => sp.GetRequiredService<StorageService>());

            })
            .Build();

        ServiceProvider = host.Services;

        // --- D. 动态编排流水线 (核心步骤) ---
        // 这一步必须在 host.Start() 之前执行，确保水管在开工前连好
        var orchestrator = host.Services.GetRequiredService<PipelineOrchestrator>();

        // 此列表未来可从 appsettings.json 动态读取
        var pipelineScheme = new List<string>
        {
            // nameof(AcquisitionService),
            // nameof(NetworkSenderNode),

            nameof(NetworkReceiverNode),
            nameof(StorageService),
            nameof(DataPushService),


            // nameof(AcquisitionService),
            // nameof(StorageService),
            // nameof(DataPushService),
            // nameof(NetworkSenderNode),
        };

        try
        {
            orchestrator.BuildPipeline(pipelineScheme);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[致命] 管道编排失败: {ex.Message}");
            return;
        }

        // 2. 启动后台引擎 (拉起各节点 ExecuteAsync 线程)
        host.Start();

        // 3. 启动 Avalonia (主线程阻塞在此)
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] UI 运行异常: {ex.Message}");
        }
        finally
        {
            // 优雅退出：通知所有节点停止工作并释放资源
            host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            host.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .UseReactiveUI();

        // 针对 Windows 7 的特殊渲染适配
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor == 1)
        {
            builder.With(new Win32PlatformOptions
            {
                CompositionMode = new[] { Win32CompositionMode.RedirectionSurface }
            });
            Console.WriteLine("[系统] 已针对 Windows 7 开启 RedirectionSurface 兼容模式");
        }
        return builder;
    }
}