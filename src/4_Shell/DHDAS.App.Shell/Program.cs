using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

// 引用契约、驱动、服务及基础设施
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using DHDAS.Contracts.Drivers;
using DHDAS.Service.Signal;
using DHDAS.App.Shell.ViewModels;
using DHDAS.App.Shell.Services;
using DHDAS.Driver.Simulator;
using DHDAS.Infrastructure.Core;
using DHDAS.Infrastructure.Core.Session;
using DHDAS.Service.Signal.Network;

namespace DHDAS.App.Shell;

class Program
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        var role = GetOption(args, "--role", "receiver").ToLowerInvariant();
        var targetIp = GetOption(args, "--target-ip", "127.0.0.1");
        var targetPort = int.TryParse(GetOption(args, "--target-port", "5000"), out var parsedPort) ? parsedPort : 5000;
        var listenPort = int.TryParse(GetOption(args, "--listen-port", "5000"), out var parsedListenPort) ? parsedListenPort : 5000;
        var channelId = int.TryParse(GetOption(args, "--channel", "0"), out var parsedChannel) ? parsedChannel : 0;
        var pipelineScheme = BuildPipelineScheme(role);

        Console.WriteLine($"[系统] 当前分布式角色: {role}");
        if (role == "receiver")
        {
            Console.WriteLine($"[网络] 接收端监听端口: {listenPort}");
        }

        // 1. 初始化 .NET Generic Host (后台引擎)
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                // --- A. 基础设施注册 ---
                services.AddSingleton<SessionManager>();
                services.AddSingleton<IDistributedFeedbackService, DistributedFeedbackService>();
                services.AddSingleton<IWaveformSnapshotService, WaveformSnapshotService>();
                services.AddSingleton(new DistributedRuntimeOptions
                {
                    Role = role,
                    TargetIp = targetIp,
                    TargetPort = targetPort,
                    ListenPort = listenPort,
                    ChannelId = channelId
                });
                services.AddSingleton<IDeviceDriver, SineWaveSimulator>();
                services.AddSingleton<PluginManager>();
                services.AddSingleton<MainWindowViewModel>();

                // --- B. 管道编排器注册 ---
                services.AddSingleton<PipelineOrchestrator>();

                // --- C. 业务节点注册 (关键：双重身份绑定) ---

                services.AddDistributionModule(pipelineScheme);

                services.AddSingleton<DHDAS.Application.Support.IMessenger, DHDAS.Application.Support.AppMessenger>();

                // 注册数据采集节点
                services.AddSingleton<AcquisitionService>();
                // 身份1：作为管道节点
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<AcquisitionService>());
                // 身份2：作为后台托管服务
                if (pipelineScheme.Contains(nameof(AcquisitionService)))
                    services.AddHostedService(sp => sp.GetRequiredService<AcquisitionService>());

                // 注册数据推送节点
                services.AddSingleton<DataPushService>();
                // 身份1：作为业务接口供应用层调用
                services.AddSingleton<IDataPushService>(sp => sp.GetRequiredService<DataPushService>());
                // 身份2：作为管道节点
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<DataPushService>());
                // 身份3：作为后台托管服务
                if (pipelineScheme.Contains(nameof(DataPushService)))
                    services.AddHostedService(sp => sp.GetRequiredService<DataPushService>());

                // 注册存储节点
                services.AddSingleton<StorageService>();
                services.AddSingleton<IPipelineNode>(sp => sp.GetRequiredService<StorageService>());
                if (pipelineScheme.Contains(nameof(StorageService)))
                    services.AddHostedService(sp => sp.GetRequiredService<StorageService>());

            })
            .Build();

        ServiceProvider = host.Services;

        // --- D. 动态编排流水线 (核心步骤) ---
        // 这一步必须在 host.Start() 之前执行，确保水管在开工前连好
        var orchestrator = host.Services.GetRequiredService<PipelineOrchestrator>();

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

    private static List<string> BuildPipelineScheme(string role)
    {
        return role switch
        {
            "sender" => new List<string>
            {
                nameof(NetworkSenderNode),
            },
            "receiver" => new List<string>
            {
                nameof(NetworkReceiverNode),
                nameof(DataPushService),
                nameof(StorageService),
            },
            _ => throw new InvalidOperationException("未知角色，请使用 --role sender 或 --role receiver。")
        };
    }

    private static string GetOption(string[] args, string name, string defaultValue)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 >= args.Length) return defaultValue;
            return args[i + 1];
        }

        return defaultValue;
    }
}
