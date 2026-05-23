using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using ReactiveUI;

namespace DHDAS.Plugin.ProjectManager.ViewModels;

public sealed class ProjectNodeViewModel : ReactiveObject
{
    private string _name;
    private ProjectMeta? _meta;
    private bool _isCurrent;

    public ProjectNodeViewModel(string name, string root)
    {
        _name = name;
        Root = root;
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public string Root { get; }

    public ProjectMeta? Meta
    {
        get => _meta;
        set => this.RaiseAndSetIfChanged(ref _meta, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => this.RaiseAndSetIfChanged(ref _isCurrent, value);
    }

    public string Description => Meta?.Description ?? string.Empty;
}

public sealed class ExperimentNodeViewModel
{
    public ExperimentNodeViewModel(ExperimentInfo info) => Info = info;

    public ExperimentInfo Info { get; }
    public string Id => Info.Id;
    public string Name => Info.Name;
    public string CreatedAtText => Info.CreatedAt.ToString("yyyy-MM-dd HH:mm");
}

public sealed class ExperimentTreeNodeViewModel
{
    public ExperimentTreeNodeViewModel(string name, string fullPath, bool isDirectory, int depth)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
        Indent = new Thickness(depth * 18, 0, 0, 0);
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public Thickness Indent { get; }
}

public sealed class ProjectManagerViewModel : PluginViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IProjectContext _projectContext;
    private readonly ISignalProcessingContext _signalProcessingContext;
    private readonly DispatcherTimer _projectRefreshTimer;
    private ProjectNodeViewModel? _selectedProject;
    private ExperimentNodeViewModel? _selectedExperiment;
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isSynchronizingView;
    private bool _isHandlingExperimentSelection;
    private bool _isPruningMissingProjects;
    private bool _syncPending;
    private string _statusMessage = "请先创建或打开工程。";
    private string _detailMessage = "左侧显示最近工程，右侧显示实验列表和详情。";
    private string _projectTitle = "未打开工程";
    private string _projectStateText = "空闲";
    private string _projectDescription = "当前没有活动工程。";
    private string _projectPathText = string.Empty;
    private string _experimentStateText = "当前实验：无";

    public ProjectManagerViewModel(
        IProjectService projectService,
        IProjectContext projectContext,
        ISignalProcessingContext signalProcessingContext)
    {
        _projectService = projectService;
        _projectContext = projectContext;
        _signalProcessingContext = signalProcessingContext;
        _projectRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _projectRefreshTimer.Tick += OnProjectRefreshTimerTick;
        _projectRefreshTimer.Start();
        _projectContext.ProjectChanged += OnProjectContextChanged;
    }

    public ObservableCollection<ProjectNodeViewModel> RecentProjects { get; } = new();
    public ObservableCollection<ExperimentNodeViewModel> Experiments { get; } = new();
    public ObservableCollection<ExperimentTreeNodeViewModel> ExperimentDetails { get; } = new();

    public ProjectNodeViewModel? SelectedProject
    {
        get => _selectedProject;
        set => this.RaiseAndSetIfChanged(ref _selectedProject, value);
    }

    public ExperimentNodeViewModel? SelectedExperiment
    {
        get => _selectedExperiment;
        set => this.RaiseAndSetIfChanged(ref _selectedExperiment, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string DetailMessage
    {
        get => _detailMessage;
        set => this.RaiseAndSetIfChanged(ref _detailMessage, value);
    }

    public string ProjectTitle
    {
        get => _projectTitle;
        set => this.RaiseAndSetIfChanged(ref _projectTitle, value);
    }

    public string ProjectStateText
    {
        get => _projectStateText;
        set => this.RaiseAndSetIfChanged(ref _projectStateText, value);
    }

    public string ProjectDescription
    {
        get => _projectDescription;
        set => this.RaiseAndSetIfChanged(ref _projectDescription, value);
    }

    public string ProjectPathText
    {
        get => _projectPathText;
        set => this.RaiseAndSetIfChanged(ref _projectPathText, value);
    }

    public string ExperimentStateText
    {
        get => _experimentStateText;
        set => this.RaiseAndSetIfChanged(ref _experimentStateText, value);
    }

    public string? CurrentProjectRoot => _projectContext.CurrentProjectRoot;

    public string? CurrentExperimentsRoot => string.IsNullOrWhiteSpace(CurrentProjectRoot)
        ? null
        : Path.Combine(CurrentProjectRoot, "Experiments");

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await SyncFromContextAsync();
    }

    public async Task PreviewProjectAsync(ProjectNodeViewModel? project)
    {
        if (project == null || _isSynchronizingView)
        {
            return;
        }

        SelectedProject = project;
        await LoadProjectDetailsAsync(project);
    }

    public async Task<bool> OpenSelectedProjectAsync()
    {
        if (SelectedProject == null)
        {
            SetFailure("打开工程", "请先选择一个工程。");
            return false;
        }

        return await OpenProjectAsync(SelectedProject.Root);
    }

    public async Task<bool> OpenProjectAsync(string projectPath)
    {
        try
        {
            await _projectService.OpenProject(projectPath);
            StatusMessage = "工程已打开。";
            DetailMessage = projectPath;
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("打开工程", ex.Message);
            return false;
        }
    }

    public async Task<bool> CreateProjectAsync(string name, string path, string author, string description)
    {
        try
        {
            var project = await _projectService.CreateProject(name, path, author, description);
            StatusMessage = $"已创建工程：{project.Name}。";
            DetailMessage = project.Path;
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("新建工程", ex.Message);
            return false;
        }
    }

    public async Task<bool> UpdateSelectedProjectAsync(string name, string description)
    {
        if (SelectedProject == null)
        {
            SetFailure("编辑工程", "请先选择一个工程。");
            return false;
        }

        try
        {
            await _projectService.UpdateProject(SelectedProject.Root, name, description);
            StatusMessage = "工程信息已更新。";
            DetailMessage = SelectedProject.Root;
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("编辑工程", ex.Message);
            return false;
        }
    }

    public async Task<bool> CloseCurrentProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectContext.CurrentProjectRoot))
        {
            SetFailure("关闭工程", "当前没有活动工程。");
            return false;
        }

        try
        {
            var closedProjectRoot = _projectContext.CurrentProjectRoot;
            await _projectService.CloseProject();
            StatusMessage = "工程已关闭。";
            DetailMessage = closedProjectRoot ?? string.Empty;
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("关闭工程", ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteSelectedProjectAsync(bool deleteFiles)
    {
        if (SelectedProject == null)
        {
            SetFailure("删除工程", "请先选择一个工程。");
            return false;
        }

        try
        {
            var projectName = SelectedProject.Name;
            await _projectService.DeleteProject(SelectedProject.Root, deleteFiles);
            StatusMessage = $"已删除工程：{projectName}。";
            DetailMessage = deleteFiles
                ? "工程目录已从磁盘删除。"
                : "工程已从列表中移除，本地文件已保留。";
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("删除工程", ex.Message);
            return false;
        }
    }

    public async Task<bool> CreateExperimentAsync(string experimentName)
    {
        if (string.IsNullOrWhiteSpace(_projectContext.CurrentProjectRoot))
        {
            SetFailure("新建实验", "请先打开一个工程。");
            return false;
        }

        try
        {
            var experiment = await _projectService.CreateExperiment(_projectContext.CurrentProjectRoot, experimentName);
            StatusMessage = $"已创建实验：{experiment.Name}。";
            DetailMessage = experiment.Path;
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("新建实验", ex.Message);
            return false;
        }
    }

    public async Task<bool> ImportExperimentAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(_projectContext.CurrentProjectRoot))
        {
            SetFailure("导入实验", "请先打开一个工程。");
            return false;
        }

        try
        {
            var experiment = await _projectService.ImportExperiment(_projectContext.CurrentProjectRoot, folderPath);
            await _projectService.ActivateExperiment(_projectContext.CurrentProjectRoot, experiment.Id);
            StatusMessage = $"已导入实验：{experiment.Name}。";
            DetailMessage = experiment.Path;
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("导入实验", ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteSelectedExperimentAsync(bool deleteFiles)
    {
        if (string.IsNullOrWhiteSpace(_projectContext.CurrentProjectRoot))
        {
            SetFailure("删除实验", "请先打开一个工程。");
            return false;
        }

        if (SelectedExperiment == null)
        {
            SetFailure("删除实验", "请先选择一个实验。");
            return false;
        }

        try
        {
            var experimentName = SelectedExperiment.Name;
            await _projectService.DeleteExperiment(
                _projectContext.CurrentProjectRoot,
                SelectedExperiment.Id,
                deleteFiles);

            StatusMessage = $"已删除实验：{experimentName}。";
            DetailMessage = deleteFiles
                ? "实验目录已从磁盘删除。"
                : "实验已从界面隐藏，本地文件已保留。";
            await SyncFromContextAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetFailure("删除实验", ex.Message);
            return false;
        }
    }

    public async Task PreviewExperimentAsync(ExperimentNodeViewModel? experiment)
    {
        if (experiment == null || _isSynchronizingView || _isHandlingExperimentSelection)
        {
            return;
        }

        _isHandlingExperimentSelection = true;
        try
        {
            SelectedExperiment = experiment;
            await LoadExperimentTreeAsync(experiment.Info.Path);

            var isCurrentProject = !string.IsNullOrWhiteSpace(_projectContext.CurrentProjectRoot) &&
                                   SelectedProject != null &&
                                   string.Equals(
                                       SelectedProject.Root,
                                       _projectContext.CurrentProjectRoot,
                                       StringComparison.OrdinalIgnoreCase);

            if (!isCurrentProject)
            {
                ExperimentStateText = $"预览实验：{experiment.Name}";
                return;
            }

            if (string.Equals(_signalProcessingContext.CurrentExperiment?.Id, experiment.Id, StringComparison.OrdinalIgnoreCase))
            {
                ExperimentStateText = $"当前实验：{experiment.Name}";
                DetailMessage = experiment.Info.Path;
                return;
            }

            try
            {
                var activated = await _projectService.ActivateExperiment(_projectContext.CurrentProjectRoot!, experiment.Id);
                if (activated != null)
                {
                    ExperimentStateText = $"当前实验：{activated.Name}";
                    StatusMessage = $"已切换到实验：{activated.Name}。";
                    DetailMessage = activated.Path;
                    return;
                }
            }
            catch (Exception ex)
            {
                SetFailure("切换实验", ex.Message);
            }

            ExperimentStateText = $"预览实验：{experiment.Name}";
        }
        finally
        {
            _isHandlingExperimentSelection = false;
        }
    }

    public override void OnDeactivated()
    {
        _projectRefreshTimer.Stop();
        _projectRefreshTimer.Tick -= OnProjectRefreshTimerTick;
        _projectContext.ProjectChanged -= OnProjectContextChanged;
        base.OnDeactivated();
    }

    private async void OnProjectRefreshTimerTick(object? sender, EventArgs e)
    {
        if (!_isInitialized || _isSynchronizingView || _isPruningMissingProjects)
        {
            return;
        }

        await PruneMissingProjectsAsync();
    }

    private void OnProjectContextChanged()
    {
        if (_isSynchronizingView)
        {
            _syncPending = true;
            return;
        }

        Dispatcher.UIThread.Post(() => _ = SyncFromContextAsync());
    }

    private async Task SyncFromContextAsync()
    {
        if (_isSynchronizingView)
        {
            _syncPending = true;
            return;
        }

        _isSynchronizingView = true;
        try
        {
            await PruneMissingProjectsAsync();

            var selectedRoot = SelectedProject?.Root;
            var currentRoot = _projectContext.CurrentProjectRoot;

            RecentProjects.Clear();
            foreach (var item in _projectContext.RecentProjects)
            {
                RecentProjects.Add(new ProjectNodeViewModel(item.Name, item.Root)
                {
                    IsCurrent = string.Equals(item.Root, currentRoot, StringComparison.OrdinalIgnoreCase)
                });
            }

            SelectedProject = RecentProjects.FirstOrDefault(project =>
                                  string.Equals(project.Root, currentRoot, StringComparison.OrdinalIgnoreCase))
                              ?? RecentProjects.FirstOrDefault(project =>
                                  string.Equals(project.Root, selectedRoot, StringComparison.OrdinalIgnoreCase))
                              ?? RecentProjects.FirstOrDefault();

            if (SelectedProject == null)
            {
                Experiments.Clear();
                ExperimentDetails.Clear();
                SelectedExperiment = null;
                ProjectTitle = "未打开工程";
                ProjectStateText = "空闲";
                ProjectDescription = "当前没有活动工程。";
                ProjectPathText = string.Empty;
                ExperimentStateText = "当前实验：无";
                return;
            }

            await LoadProjectDetailsAsync(SelectedProject);
        }
        finally
        {
            _isSynchronizingView = false;
        }

        if (_syncPending)
        {
            _syncPending = false;
            await SyncFromContextAsync();
        }
    }

    private async Task LoadProjectDetailsAsync(ProjectNodeViewModel project)
    {
        IsBusy = true;
        try
        {
            if (!ProjectExists(project.Root))
            {
                await PruneMissingProjectsAsync(project.Root);
                return;
            }

            foreach (var node in RecentProjects)
            {
                node.IsCurrent = string.Equals(node.Root, _projectContext.CurrentProjectRoot, StringComparison.OrdinalIgnoreCase);
            }

            var meta = await _projectService.GetProjectMeta(project.Root);
            project.Meta = meta;
            if (meta != null)
            {
                project.Name = meta.Name;
            }

            ProjectTitle = meta?.Name ?? project.Name;
            ProjectStateText = project.IsCurrent ? "当前工程" : "最近工程";
            ProjectDescription = string.IsNullOrWhiteSpace(meta?.Description)
                ? "暂无工程说明。"
                : meta!.Description;
            ProjectPathText = $"工程路径：{project.Root}";

            Experiments.Clear();
            foreach (var experiment in await _projectService.GetExperiments(project.Root))
            {
                Experiments.Add(new ExperimentNodeViewModel(experiment));
            }

            var preferredExperimentId = project.IsCurrent
                ? _signalProcessingContext.CurrentExperiment?.Id ?? meta?.LastActiveExperimentId
                : meta?.LastActiveExperimentId;

            if (!string.IsNullOrWhiteSpace(preferredExperimentId))
            {
                var preferredNode = Experiments.FirstOrDefault(item =>
                    string.Equals(item.Id, preferredExperimentId, StringComparison.OrdinalIgnoreCase));

                if (preferredNode != null)
                {
                    SelectedExperiment = preferredNode;
                    await LoadExperimentTreeAsync(preferredNode.Info.Path);
                    ExperimentStateText = project.IsCurrent
                        ? $"当前实验：{preferredNode.Name}"
                        : $"上次实验：{preferredNode.Name}";
                }
                else
                {
                    SelectedExperiment = null;
                    ExperimentDetails.Clear();
                    ExperimentStateText = "当前实验：无";
                }
            }
            else
            {
                SelectedExperiment = null;
                ExperimentDetails.Clear();
                ExperimentStateText = "当前实验：无";
            }

            DetailMessage = Experiments.Count == 0
                ? "当前工程下没有可见实验。"
                : $"已加载 {Experiments.Count} 个实验。";
        }
        catch (Exception ex)
        {
            Experiments.Clear();
            ExperimentDetails.Clear();
            SelectedExperiment = null;
            SetFailure("加载工程", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> PruneMissingProjectsAsync(string? projectRoot = null)
    {
        if (_isPruningMissingProjects)
        {
            return false;
        }

        _isPruningMissingProjects = true;
        try
        {
            var missingRoots = _projectContext.RecentProjects
                .Select(item => item.Root)
                .Where(root => !ProjectExists(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(projectRoot) &&
                !ProjectExists(projectRoot) &&
                !missingRoots.Contains(projectRoot, StringComparer.OrdinalIgnoreCase))
            {
                missingRoots.Insert(0, projectRoot);
            }

            if (missingRoots.Count == 0)
            {
                return false;
            }

            var currentProjectRemoved = false;
            foreach (var missingRoot in missingRoots)
            {
                if (string.Equals(
                        _projectContext.CurrentProjectRoot,
                        missingRoot,
                        StringComparison.OrdinalIgnoreCase))
                {
                    currentProjectRemoved = true;
                    await _projectService.CloseProject();
                }

                _projectContext.RemoveProject(missingRoot);
            }

            StatusMessage = currentProjectRemoved
                ? "\u5de5\u7a0b\u76ee\u5f55\u5df2\u4e0d\u5b58\u5728\uff0c\u5f53\u524d\u5de5\u7a0b\u5df2\u5173\u95ed\u3002"
                : "\u68c0\u6d4b\u5230\u5931\u6548\u5de5\u7a0b\uff0c\u5df2\u4ece\u5217\u8868\u4e2d\u79fb\u9664\u3002";
            DetailMessage = missingRoots.Count == 1
                ? missingRoots[0]
                : $"\u5df2\u6e05\u7406 {missingRoots.Count} \u4e2a\u5931\u6548\u5de5\u7a0b\u3002";

            return true;
        }
        catch (Exception ex)
        {
            SetFailure("\u5237\u65b0\u5de5\u7a0b\u5217\u8868", ex.Message);
            return false;
        }
        finally
        {
            _isPruningMissingProjects = false;
        }
    }

    private async Task LoadExperimentTreeAsync(string experimentPath)
    {
        ExperimentDetails.Clear();
        var entries = await _projectService.GetExperimentDirectoryTree(experimentPath);
        foreach (var entry in entries)
        {
            AppendEntry(entry, 0);
        }
    }

    private void AppendEntry(ProjectFileEntry entry, int depth)
    {
        ExperimentDetails.Add(new ExperimentTreeNodeViewModel(entry.Name, entry.FullPath, entry.IsDirectory, depth));
        foreach (var child in entry.Children)
        {
            AppendEntry(child, depth + 1);
        }
    }

    private static bool ProjectExists(string root) =>
        Directory.Exists(root) && File.Exists(Path.Combine(root, "project.json"));

    private void SetFailure(string actionName, string message)
    {
        StatusMessage = $"{actionName}失败。";
        DetailMessage = message;
    }
}
