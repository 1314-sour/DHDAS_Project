using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using DHDAS.Application.Support;
using System.Runtime.Loader;

namespace DHDAS.App.Shell.Services;

public class PluginManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<PluginBase> _plugins = new();

    public PluginManager(IServiceProvider sp) => _serviceProvider = sp;

    public void LoadPlugins()
    {
        // 获取 EXE 所在的物理路径
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // 强制拼接 Plugins 文件夹
        string pluginRoot = Path.Combine(baseDir, "Plugins");

        Console.WriteLine($"[系统] 正在扫描插件目录: {pluginRoot}");

        if (!Directory.Exists(pluginRoot)) return;

        var dllFiles = Directory.GetFiles(pluginRoot, "DHDAS.Plugin.*.dll", SearchOption.AllDirectories);
        foreach (var file in dllFiles)
        {
            try
            {
                // 使用自定义加载上下文（见下一步）
                var pluginAssembly = LoadPlugin(file);

                var pluginType = pluginAssembly.GetTypes()
                    .FirstOrDefault(t => typeof(PluginBase).IsAssignableFrom(t) && !t.IsAbstract);

                if (pluginType != null)
                {
                    var plugin = (PluginBase)Activator.CreateInstance(pluginType)!;
                    _plugins.Add(plugin);
                    Console.WriteLine($"[系统] 已成功加载商业插件: {plugin.DisplayName} v{pluginAssembly.GetName().Version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[系统] 插件 {Path.GetFileName(file)} 加载失败: {ex.Message}");
            }
        }
    }

    private Assembly LoadPlugin(string pluginPath)
    {
        // 为每个插件创建一个独立的“隔离舱”
        var loadContext = new PluginLoadContext(pluginPath);
        return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginPath)));
    }

    // 定义一个隔离加载类
    class PluginLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // 如果主程序已经有了（比如 Contracts, Support），就用主程序的
            // 否则去插件目录找
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            return null;
        }
    }

    public IEnumerable<PluginBase> GetPlugins() => _plugins;
}