using System.Text.Json;
using DHDAS.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace DHDAS.Service.Signal.Project;

public sealed class ProjectContext : IProjectContext
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly ILogger<ProjectContext> _logger;
    private readonly List<(string Name, string Root)> _recentProjects = new();
    private readonly string _indexFilePath;

    public ProjectContext(ILogger<ProjectContext> logger)
    {
        _logger = logger;
        _indexFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DHDAS",
            "recent-projects.json");

        LoadIndex();
    }

    public string? CurrentProjectRoot { get; private set; }
    public string? CurrentProjectName { get; private set; }
    public IReadOnlyList<(string Name, string Root)> RecentProjects => _recentProjects;
    public event Action? ProjectChanged;

    public void SetCurrentProject(string name, string root)
    {
        CurrentProjectName = name;
        CurrentProjectRoot = root;
        UpsertEntry(name, root, makeCurrent: true);
    }

    public void UpdateProjectEntry(string name, string root)
    {
        var isCurrent = string.Equals(CurrentProjectRoot, root, StringComparison.OrdinalIgnoreCase);
        if (isCurrent)
        {
            CurrentProjectName = name;
        }

        UpsertEntry(name, root, makeCurrent: isCurrent);
    }

    public void RemoveProject(string root)
    {
        var removed = _recentProjects.RemoveAll(item =>
            string.Equals(item.Root, root, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(CurrentProjectRoot, root, StringComparison.OrdinalIgnoreCase))
        {
            CurrentProjectRoot = null;
            CurrentProjectName = null;
        }

        if (removed > 0)
        {
            SaveIndex();
            ProjectChanged?.Invoke();
        }
    }

    public void ClearCurrentProject()
    {
        if (CurrentProjectRoot == null && CurrentProjectName == null)
        {
            return;
        }

        CurrentProjectRoot = null;
        CurrentProjectName = null;
        SaveIndex();
        ProjectChanged?.Invoke();
    }

    private void UpsertEntry(string name, string root, bool makeCurrent)
    {
        var existingIndex = _recentProjects.FindIndex(item =>
            string.Equals(item.Root, root, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            _recentProjects.RemoveAt(existingIndex);
        }

        if (makeCurrent)
        {
            _recentProjects.Insert(0, (name, root));
        }
        else if (existingIndex >= 0 && existingIndex <= _recentProjects.Count)
        {
            _recentProjects.Insert(existingIndex, (name, root));
        }
        else
        {
            _recentProjects.Add((name, root));
        }

        if (_recentProjects.Count > 10)
        {
            _recentProjects.RemoveRange(10, _recentProjects.Count - 10);
        }

        SaveIndex();
        ProjectChanged?.Invoke();
    }

    private void LoadIndex()
    {
        try
        {
            if (!File.Exists(_indexFilePath))
            {
                return;
            }

            var json = File.ReadAllText(_indexFilePath);
            var items = JsonSerializer.Deserialize<List<ProjectIndexEntry>>(json, JsonOptions);
            if (items == null)
            {
                return;
            }

            var indexChanged = false;
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Root))
                {
                    indexChanged = true;
                    continue;
                }

                var normalizedRoot = Path.GetFullPath(item.Root);
                if (!ProjectExists(normalizedRoot))
                {
                    indexChanged = true;
                    continue;
                }

                _recentProjects.Add((item.Name, normalizedRoot));
            }

            if (indexChanged)
            {
                SaveIndex();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载最近工程索引失败: {IndexFilePath}", _indexFilePath);
        }
    }

    private void SaveIndex()
    {
        try
        {
            var directory = Path.GetDirectoryName(_indexFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var items = _recentProjects
                .Select(item => new ProjectIndexEntry { Name = item.Name, Root = item.Root })
                .ToList();

            File.WriteAllText(_indexFilePath, JsonSerializer.Serialize(items, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存最近工程索引失败: {IndexFilePath}", _indexFilePath);
        }
    }

    private sealed class ProjectIndexEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Root { get; set; } = string.Empty;
    }

    private static bool ProjectExists(string root) =>
        Directory.Exists(root) && File.Exists(Path.Combine(root, "project.json"));
}
