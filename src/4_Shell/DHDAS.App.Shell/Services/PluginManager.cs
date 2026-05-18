using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using DHDAS.Application.Support;

namespace DHDAS.App.Shell.Services;

public class PluginManager : IDisposable
{
    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "DHDAS.Contracts",
        "DHDAS.Application.Support",
        "Avalonia",
        "Avalonia.Base",
        "Avalonia.Controls",
        "Avalonia.Controls.DataGrid",
        "Avalonia.Desktop",
        "Avalonia.Markup.Xaml",
        "Avalonia.ReactiveUI",
        "ReactiveUI",
        "System.Reactive",
        "Microsoft.Extensions.DependencyInjection.Abstractions"
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly List<PluginHandle> _handles = new();

    public PluginManager(IServiceProvider sp) => _serviceProvider = sp;

    public IReadOnlyList<PluginBase> LoadPlugins()
    {
        var pluginRoot = GetPluginRoot();
        Console.WriteLine($"[Plugin] Scanning plugin directory: {pluginRoot}");

        if (!Directory.Exists(pluginRoot))
        {
            Directory.CreateDirectory(pluginRoot);
            return GetPlugins().ToArray();
        }

        var dllFiles = Directory
            .GetFiles(pluginRoot, "DHDAS.Plugin.*.dll", SearchOption.AllDirectories)
            .OrderBy(path => path)
            .ToArray();

        foreach (var file in dllFiles)
        {
            if (_handles.Any(handle => string.Equals(handle.PluginPath, file, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            TryLoadPlugin(file);
        }

        return GetPlugins().ToArray();
    }

    public IEnumerable<PluginBase> GetPlugins() =>
        _handles
            .Select(handle => handle.Plugin)
            .OrderBy(plugin => plugin.Priority)
            .ThenBy(plugin => plugin.DisplayName);

    public bool UnloadPlugin(PluginBase plugin)
    {
        var handle = _handles.FirstOrDefault(item => ReferenceEquals(item.Plugin, plugin));
        if (handle == null)
        {
            return false;
        }

        _handles.Remove(handle);
        Console.WriteLine($"[Plugin] Unloading plugin: {plugin.DisplayName}");

        try
        {
            handle.Plugin.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Plugin] Plugin dispose failed: {plugin.DisplayName}, {ex.Message}");
        }

        handle.LoadContext.Unload();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return true;
    }

    public void UnloadAll()
    {
        foreach (var plugin in GetPlugins().ToArray())
        {
            UnloadPlugin(plugin);
        }
    }

    public void Dispose() => UnloadAll();

    private void TryLoadPlugin(string pluginPath)
    {
        try
        {
            var loadContext = new PluginLoadContext(pluginPath);
            var pluginAssembly = loadContext.LoadFromAssemblyName(
                new AssemblyName(Path.GetFileNameWithoutExtension(pluginPath)));

            var pluginType = pluginAssembly.GetTypes()
                .FirstOrDefault(t => typeof(PluginBase).IsAssignableFrom(t) && !t.IsAbstract);

            if (pluginType == null)
            {
                loadContext.Unload();
                return;
            }

            var plugin = (PluginBase)Activator.CreateInstance(pluginType)!;
            plugin.OnLoaded(_serviceProvider);
            _handles.Add(new PluginHandle(pluginPath, plugin, loadContext));

            Console.WriteLine($"[Plugin] Loaded {plugin.DisplayName} from {Path.GetFileName(pluginPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Plugin] Failed to load {Path.GetFileName(pluginPath)}: {ex.Message}");
        }
    }

    private static string GetPluginRoot() =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

    private sealed class PluginHandle
    {
        public PluginHandle(string pluginPath, PluginBase plugin, AssemblyLoadContext loadContext)
        {
            PluginPath = pluginPath;
            Plugin = plugin;
            LoadContext = loadContext;
        }

        public string PluginPath { get; }
        public PluginBase Plugin { get; }
        public AssemblyLoadContext LoadContext { get; }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name != null && SharedAssemblies.Contains(assemblyName.Name))
            {
                return null;
            }

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }
    }
}
