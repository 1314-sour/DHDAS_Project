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
    private bool _mounted;

    public PluginManager(IServiceProvider sp, IEnumerable<PluginBase> plugins)
    {
        _serviceProvider = sp;
        _plugins.AddRange(plugins);
    }

    public void LoadPlugins()
    {
        if (_plugins.Count == 0)
        {
            _plugins.AddRange(DiscoverPlugins(GetDefaultPluginRoot()));
        }

        if (_mounted) return;

        foreach (var plugin in _plugins.OrderBy(p => p.Priority))
        {
            plugin.Initialize(_serviceProvider);
            plugin.Mount(_serviceProvider);
        }

        _mounted = true;
    }

    public static string GetDefaultPluginRoot()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
    }

    public static IReadOnlyList<PluginBase> DiscoverPlugins(string pluginRoot)
    {
        Console.WriteLine($"[系统] 正在扫描插件目录: {pluginRoot}");

        var plugins = new List<PluginBase>();
        if (!Directory.Exists(pluginRoot)) return plugins;

        var dllFiles = Directory.GetFiles(pluginRoot, "DHDAS.Plugin.*.dll", SearchOption.AllDirectories);
        foreach (var file in dllFiles)
        {
            try
            {
                var pluginAssembly = LoadPlugin(file);
                var pluginTypes = pluginAssembly.GetTypes()
                    .Where(t => typeof(PluginBase).IsAssignableFrom(t) && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    var plugin = (PluginBase)Activator.CreateInstance(pluginType)!;
                    plugins.Add(plugin);
                    Console.WriteLine($"[系统] 已发现插件: {plugin.DisplayName} v{plugin.Version}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[系统] 插件 {Path.GetFileName(file)} 加载失败: {ex.Message}");
            }
        }

        return plugins;
    }

    public void Shutdown()
    {
        foreach (var plugin in _plugins.OrderByDescending(p => p.Priority))
        {
            try
            {
                plugin.Unmount();
                plugin.Destroy();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[系统] 插件 {plugin.DisplayName} 卸载失败: {ex.Message}");
            }
        }

        _mounted = false;
    }

    private static Assembly LoadPlugin(string pluginPath)
    {
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
