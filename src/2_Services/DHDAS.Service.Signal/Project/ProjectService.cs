using System.Text.Json;
using DHDAS.Application.Support;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using DHDAS.Service.Signal.Instrument.Domain;
using Microsoft.Extensions.Logging;

namespace DHDAS.Service.Signal.Project;

public sealed class ProjectService : IProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim _projectMetadataLock = new(1, 1);
    private readonly object _channelSnapshotLock = new();
    private readonly IProjectContext _projectContext;
    private readonly IInstrumentService _instrumentService;
    private readonly ISignalProcessingContext _signalProcessingContext;
    private readonly IMessenger _messenger;
    private readonly ILogger<ProjectService> _logger;
    private Dictionary<int, ChannelInfo> _channelSnapshot;

    public ProjectService(
        IProjectContext projectContext,
        IInstrumentService instrumentService,
        ISignalProcessingContext signalProcessingContext,
        IMessenger messenger,
        ILogger<ProjectService> logger)
    {
        _projectContext = projectContext;
        _instrumentService = instrumentService;
        _signalProcessingContext = signalProcessingContext;
        _messenger = messenger;
        _logger = logger;
        _channelSnapshot = BuildChannelSnapshot(_instrumentService.GetChannelSettings());

        messenger.Listen<ChannelSettingsChangedMessage>()
            .Subscribe(message => _ = HandleChannelSettingsChangedAsync(message));
    }

    public async Task<ProjectMeta> CreateProject(string name, string path, string author = "", string description = "")
    {
        try
        {
            var projectName = ValidateName(name, "Project name");
            var rootPath = ValidateBasePath(path);
            var projectRoot = Path.Combine(rootPath, projectName);

            if (Directory.Exists(projectRoot) && Directory.EnumerateFileSystemEntries(projectRoot).Any())
            {
                throw new InvalidOperationException($"Project directory already exists: {projectRoot}");
            }

            Directory.CreateDirectory(projectRoot);
            foreach (var folder in ProjectStructure.RequiredFolders)
            {
                Directory.CreateDirectory(Path.Combine(projectRoot, folder));
            }

            var meta = new ProjectMeta
            {
                Name = projectName,
                Path = projectRoot,
                Author = author.Trim(),
                Description = description.Trim(),
                CreatedAt = DateTime.Now,
                Version = "2.0"
            };

            var channels = GetCurrentChannelSnapshot();
            await WriteMetaAsync(projectRoot, meta);
            await SaveChannelConfig(projectRoot, channels);

            _projectContext.SetCurrentProject(meta.Name, projectRoot);
            _signalProcessingContext.NotifyExperimentSwitched(null);
            PublishProjectChanged(meta, null, ProjectChangeKind.Opened);

            _logger.LogInformation("Created project: {ProjectName} -> {ProjectRoot}", meta.Name, projectRoot);
            return meta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project: {ProjectName}", name);
            throw;
        }
    }

    public async Task<ProjectMeta> OpenProject(string path)
    {
        try
        {
            var projectRoot = Path.GetFullPath(path.Trim());
            var meta = await ReadMetaAsync(projectRoot);

            _projectContext.SetCurrentProject(meta.Name, projectRoot);

            var channelsFileExists = File.Exists(ProjectStructure.ProjectChannelsFile(projectRoot));
            var channels = await LoadChannelConfig(projectRoot);
            if (channelsFileExists)
            {
                var runtimeChannels = BuildRuntimeChannelSnapshot(channels);
                _instrumentService.ApplySettings(runtimeChannels.ToList());
                ReplaceChannelSnapshot(runtimeChannels);
                _logger.LogInformation("Restored project channel config: {ChannelCount}", channels.Count);
            }
            else
            {
                ReplaceChannelSnapshot(_instrumentService.GetChannelSettings());
            }

            ExperimentInfo? experiment = null;
            if (!string.IsNullOrWhiteSpace(meta.LastActiveExperimentId))
            {
                experiment = await TryReadExperimentAsync(projectRoot, meta.LastActiveExperimentId);
            }

            _signalProcessingContext.NotifyExperimentSwitched(experiment);
            _messenger.Send(new ExperimentSwitchedMessage(experiment));
            PublishProjectChanged(meta, experiment, ProjectChangeKind.Opened);

            _logger.LogInformation("Opened project: {ProjectName} -> {ProjectRoot}", meta.Name, projectRoot);
            return meta;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open project: {ProjectRoot}", path);
            throw;
        }
    }

    public async Task CloseProject()
    {
        try
        {
            var currentProjectRoot = _projectContext.CurrentProjectRoot;
            if (string.IsNullOrWhiteSpace(currentProjectRoot))
            {
                return;
            }

            await CloseProjectInternalAsync(ProjectChangeKind.Closed);
            _logger.LogInformation("Closed project: {ProjectRoot}", currentProjectRoot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to close project");
            throw;
        }
    }

    public async Task UpdateProject(string projectRoot, string name, string description)
    {
        try
        {
            var meta = await ReadMetaAsync(projectRoot);
            meta.Name = ValidateName(name, "Project name");
            meta.Description = description.Trim();

            await WriteMetaAsync(projectRoot, meta);
            _projectContext.UpdateProjectEntry(meta.Name, projectRoot);

            _logger.LogInformation("Updated project: {ProjectRoot}", projectRoot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update project: {ProjectRoot}", projectRoot);
            throw;
        }
    }

    public async Task DeleteProject(string projectRoot, bool deleteFiles = false)
    {
        try
        {
            var normalizedRoot = Path.GetFullPath(projectRoot);
            if (string.Equals(_projectContext.CurrentProjectRoot, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                await CloseProjectInternalAsync(ProjectChangeKind.Deleted);
            }

            _projectContext.RemoveProject(normalizedRoot);

            if (deleteFiles && Directory.Exists(normalizedRoot))
            {
                Directory.Delete(normalizedRoot, recursive: true);
            }

            _logger.LogInformation(
                "Deleted project: {ProjectRoot}, DeleteFiles={DeleteFiles}",
                normalizedRoot,
                deleteFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project: {ProjectRoot}", projectRoot);
            throw;
        }
    }

    public async Task<ProjectMeta?> GetProjectMeta(string projectRoot)
    {
        try
        {
            var metadataFile = ProjectStructure.MetadataFile(projectRoot);
            if (!File.Exists(metadataFile))
            {
                return null;
            }

            return await ReadMetaAsync(projectRoot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read project metadata: {ProjectRoot}", projectRoot);
            return null;
        }
    }

    public async Task SaveLastOpenedExperiment(string projectRoot, string experimentId)
    {
        try
        {
            var meta = await ReadMetaAsync(projectRoot);
            meta.LastOpenedExperiment = experimentId;
            meta.LastActiveExperimentId = experimentId;
            await WriteMetaAsync(projectRoot, meta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save last experiment: {ProjectRoot}, {ExperimentId}", projectRoot, experimentId);
            throw;
        }
    }

    public async Task<List<ExperimentInfo>> GetExperiments(string projectRoot)
    {
        try
        {
            var meta = await ReadMetaAsync(projectRoot);
            var experiments = new List<ExperimentInfo>();

            foreach (var experimentId in meta.ExperimentIds)
            {
                var experiment = await TryReadExperimentAsync(projectRoot, experimentId);
                if (experiment != null)
                {
                    experiments.Add(experiment);
                }
            }

            return experiments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read experiment list: {ProjectRoot}", projectRoot);
            return new List<ExperimentInfo>();
        }
    }

    public async Task<ExperimentInfo> CreateExperiment(string projectRoot, string experimentName)
    {
        try
        {
            var experimentId = ValidateName(experimentName, "Experiment name");
            var meta = await ReadMetaAsync(projectRoot);
            var experimentRoot = ProjectStructure.ExperimentRoot(projectRoot, experimentId);

            if (Directory.Exists(experimentRoot))
            {
                throw new InvalidOperationException($"Experiment already exists: {experimentId}");
            }

            Directory.CreateDirectory(experimentRoot);
            Directory.CreateDirectory(ProjectStructure.ExperimentRawDataFolder(experimentRoot));
            Directory.CreateDirectory(ProjectStructure.ExperimentResultsFolder(experimentRoot));
            Directory.CreateDirectory(ProjectStructure.ExperimentConfigFolder(experimentRoot));

            var channels = GetCurrentChannelSnapshot();
            var persistedChannels = BuildPersistedChannelList(channels);
            var experiment = new ExperimentInfo
            {
                Id = experimentId,
                Name = experimentName.Trim(),
                Path = experimentRoot,
                CreatedAt = DateTime.Now,
                Config = new ExperimentConfig
                {
                    ChannelSettings = persistedChannels.ToList()
                }
            };

            await WriteExperimentAsync(experimentRoot, experiment);
            await SaveExperimentChannelsAsync(experimentRoot, channels);

            if (!meta.ExperimentIds.Contains(experiment.Id, StringComparer.OrdinalIgnoreCase))
            {
                meta.ExperimentIds.Add(experiment.Id);
            }

            meta.LastOpenedExperiment = experiment.Id;
            meta.LastActiveExperimentId = experiment.Id;
            await WriteMetaAsync(projectRoot, meta);

            _signalProcessingContext.NotifyExperimentSwitched(experiment);
            _messenger.Send(new ExperimentSwitchedMessage(experiment));
            PublishProjectChanged(meta, experiment, ProjectChangeKind.ExperimentChanged);

            _logger.LogInformation("Created experiment: {ExperimentId}", experiment.Id);
            return experiment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create experiment: {ProjectRoot}, {ExperimentName}", projectRoot, experimentName);
            throw;
        }
    }

    public async Task<ExperimentInfo> ImportExperiment(string projectRoot, string experimentFolderPath)
    {
        try
        {
            var expectedRoot = Path.GetFullPath(ProjectStructure.ExperimentsRoot(projectRoot));
            var folderPath = Path.GetFullPath(experimentFolderPath);
            EnsureUnderRoot(folderPath, expectedRoot);

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Experiment directory does not exist: {folderPath}");
            }

            var experiment = await ReadExperimentByPathAsync(folderPath);
            var meta = await ReadMetaAsync(projectRoot);

            if (!meta.ExperimentIds.Contains(experiment.Id, StringComparer.OrdinalIgnoreCase))
            {
                meta.ExperimentIds.Add(experiment.Id);
                await WriteMetaAsync(projectRoot, meta);
            }

            _logger.LogInformation("Imported experiment: {ExperimentId}", experiment.Id);
            return experiment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import experiment: {ProjectRoot}, {ExperimentFolderPath}", projectRoot, experimentFolderPath);
            throw;
        }
    }

    public async Task<ExperimentInfo?> ActivateExperiment(string projectRoot, string experimentId)
    {
        try
        {
            var normalizedProjectRoot = Path.GetFullPath(projectRoot);
            if (string.Equals(_projectContext.CurrentProjectRoot, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_signalProcessingContext.CurrentExperiment?.Id, experimentId, StringComparison.OrdinalIgnoreCase))
            {
                return _signalProcessingContext.CurrentExperiment;
            }

            var experiment = await TryReadExperimentAsync(normalizedProjectRoot, experimentId);
            if (experiment == null)
            {
                return null;
            }

            if (_signalProcessingContext.OnExperimentSwitching != null)
            {
                var allowSwitch = await _signalProcessingContext.OnExperimentSwitching(experiment);
                if (!allowSwitch)
                {
                    _logger.LogInformation("Experiment switch was blocked: {ExperimentId}", experiment.Id);
                    return null;
                }
            }

            var meta = await ReadMetaAsync(normalizedProjectRoot);
            meta.LastOpenedExperiment = experiment.Id;
            meta.LastActiveExperimentId = experiment.Id;
            await WriteMetaAsync(normalizedProjectRoot, meta);

            _signalProcessingContext.NotifyExperimentSwitched(experiment);
            _messenger.Send(new ExperimentSwitchedMessage(experiment));
            PublishProjectChanged(meta, experiment, ProjectChangeKind.ExperimentChanged);

            _logger.LogInformation("Activated experiment: {ExperimentId}", experiment.Id);
            return experiment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate experiment: {ProjectRoot}, {ExperimentId}", projectRoot, experimentId);
            throw;
        }
    }

    public async Task DeleteExperiment(string projectRoot, string experimentId, bool deleteFiles = false)
    {
        try
        {
            var meta = await ReadMetaAsync(projectRoot);
            var experiment = await TryReadExperimentAsync(projectRoot, experimentId);

            if (experiment == null)
            {
                return;
            }

            if (string.Equals(_signalProcessingContext.CurrentExperiment?.Id, experimentId, StringComparison.OrdinalIgnoreCase))
            {
                await PersistCurrentChannelStateAsync(projectRoot, experiment);
                _signalProcessingContext.NotifyExperimentSwitched(null);
                _messenger.Send(new ExperimentSwitchedMessage(null));
                PublishProjectChanged(meta, null, ProjectChangeKind.ExperimentChanged);
            }

            meta.ExperimentIds.RemoveAll(id => string.Equals(id, experimentId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(meta.LastOpenedExperiment, experimentId, StringComparison.OrdinalIgnoreCase))
            {
                meta.LastOpenedExperiment = null;
            }

            if (string.Equals(meta.LastActiveExperimentId, experimentId, StringComparison.OrdinalIgnoreCase))
            {
                meta.LastActiveExperimentId = null;
            }

            await WriteMetaAsync(projectRoot, meta);

            if (deleteFiles && Directory.Exists(experiment.Path))
            {
                Directory.Delete(experiment.Path, recursive: true);
            }

            _logger.LogInformation(
                "Deleted experiment: {ExperimentId}, DeleteFiles={DeleteFiles}",
                experimentId,
                deleteFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete experiment: {ProjectRoot}, {ExperimentId}", projectRoot, experimentId);
            throw;
        }
    }

    public async Task SaveExperimentConfig(string experimentPath, ExperimentConfig config)
    {
        try
        {
            var experiment = await ReadExperimentByPathAsync(experimentPath);
            experiment.Config = config;
            await WriteExperimentAsync(experimentPath, experiment);

            _logger.LogInformation("Saved experiment config: {ExperimentPath}", experimentPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save experiment config: {ExperimentPath}", experimentPath);
            throw;
        }
    }

    public async Task SaveChannelConfig(string projectRoot, IReadOnlyList<ChannelInfo> channels)
    {
        try
        {
            var persistedChannels = BuildPersistedChannelList(channels);
            Directory.CreateDirectory(ProjectStructure.ProjectConfigFolder(projectRoot));
            await File.WriteAllTextAsync(
                ProjectStructure.ProjectChannelsFile(projectRoot),
                JsonSerializer.Serialize(persistedChannels, JsonOptions));

            _logger.LogInformation("Saved project channel config: {ProjectRoot}", projectRoot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project channel config: {ProjectRoot}", projectRoot);
            throw;
        }
    }

    public async Task<IReadOnlyList<ChannelInfo>> LoadChannelConfig(string projectRoot)
    {
        try
        {
            var filePath = ProjectStructure.ProjectChannelsFile(projectRoot);
            if (!File.Exists(filePath))
            {
                return Array.Empty<ChannelInfo>();
            }

            var channels = JsonSerializer.Deserialize<List<ChannelInfo>>(
                               await File.ReadAllTextAsync(filePath),
                               JsonOptions)
                           ?? new List<ChannelInfo>();

            return BuildPersistedChannelList(channels);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read project channel config: {ProjectRoot}", projectRoot);
            return Array.Empty<ChannelInfo>();
        }
    }

    public async Task<IReadOnlyList<ProjectFileEntry>> GetExperimentDirectoryTree(string experimentPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(experimentPath);
            return await Task.FromResult<IReadOnlyList<ProjectFileEntry>>(new List<ProjectFileEntry>
            {
                BuildDirectoryEntry(ProjectStructure.ExperimentRawDataFolder(fullPath)),
                BuildDirectoryEntry(ProjectStructure.ExperimentResultsFolder(fullPath))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load experiment detail tree: {ExperimentPath}", experimentPath);
            return Array.Empty<ProjectFileEntry>();
        }
    }

    private async Task CloseProjectInternalAsync(ProjectChangeKind changeKind)
    {
        var currentProjectRoot = _projectContext.CurrentProjectRoot;
        if (string.IsNullOrWhiteSpace(currentProjectRoot))
        {
            return;
        }

        var currentExperiment = _signalProcessingContext.CurrentExperiment;
        ProjectMeta? meta = null;
        if (ProjectExists(currentProjectRoot))
        {
            await PersistCurrentChannelStateAsync(currentProjectRoot, currentExperiment);
            meta = await GetProjectMeta(currentProjectRoot);
        }

        _signalProcessingContext.NotifyExperimentSwitched(null);
        _messenger.Send(new ExperimentSwitchedMessage(null));
        PublishProjectChanged(meta, null, changeKind);
        _projectContext.ClearCurrentProject();
    }

    private async Task PersistChannelSnapshotAsync(IReadOnlyList<ChannelInfo> channels)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_projectContext.CurrentProjectRoot))
            {
                await SaveChannelConfig(_projectContext.CurrentProjectRoot, channels);
            }

            if (_signalProcessingContext.CurrentExperiment != null)
            {
                await SaveExperimentChannelsAsync(_signalProcessingContext.CurrentExperiment.Path, channels);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist channel snapshot");
        }
    }

    private async Task HandleChannelSettingsChangedAsync(ChannelSettingsChangedMessage message)
    {
        var channels = message.Channels.Count > 0
            ? MergeChannelSnapshot(message.Channels)
            : CaptureCurrentChannels();

        await PersistChannelSnapshotAsync(channels);
    }

    private async Task PersistCurrentChannelStateAsync(string projectRoot, ExperimentInfo? experiment)
    {
        var channels = GetCurrentChannelSnapshot();
        await SaveChannelConfig(projectRoot, channels);

        if (experiment != null)
        {
            await SaveExperimentChannelsAsync(experiment.Path, channels);
        }
    }

    private IReadOnlyList<ChannelInfo> CaptureCurrentChannels()
    {
        var channels = _instrumentService.GetChannelSettings().ToList();
        ReplaceChannelSnapshot(channels);
        return channels;
    }

    private IReadOnlyList<ChannelInfo> GetCurrentChannelSnapshot()
    {
        lock (_channelSnapshotLock)
        {
            if (_channelSnapshot.Count == 0)
            {
                return Array.Empty<ChannelInfo>();
            }

            return _channelSnapshot.Values
                .OrderBy(channel => channel.ChannelId)
                .ToArray();
        }
    }

    private IReadOnlyList<ChannelInfo> MergeChannelSnapshot(IReadOnlyList<ChannelInfo> channels)
    {
        lock (_channelSnapshotLock)
        {
            foreach (var channel in channels)
            {
                _channelSnapshot[channel.ChannelId] = channel;
            }

            return _channelSnapshot.Values
                .OrderBy(channel => channel.ChannelId)
                .ToArray();
        }
    }

    private void ReplaceChannelSnapshot(IEnumerable<ChannelInfo> channels)
    {
        lock (_channelSnapshotLock)
        {
            _channelSnapshot = BuildChannelSnapshot(channels);
        }
    }

    private static Dictionary<int, ChannelInfo> BuildChannelSnapshot(IEnumerable<ChannelInfo> channels) =>
        channels
            .OrderBy(channel => channel.ChannelId)
            .ToDictionary(channel => channel.ChannelId, channel => channel);

    private async Task SaveExperimentChannelsAsync(string experimentPath, IReadOnlyList<ChannelInfo> channels)
    {
        var persistedChannels = BuildPersistedChannelList(channels);
        Directory.CreateDirectory(ProjectStructure.ExperimentConfigFolder(experimentPath));
        await File.WriteAllTextAsync(
            ProjectStructure.ExperimentChannelsFile(experimentPath),
            JsonSerializer.Serialize(persistedChannels, JsonOptions));
    }

    private static IReadOnlyList<ChannelInfo> BuildPersistedChannelList(IEnumerable<ChannelInfo> channels) =>
        channels
            .Where(channel => channel.IsEnabled)
            .OrderBy(channel => channel.ChannelId)
            .ToArray();

    private static IReadOnlyList<ChannelInfo> BuildRuntimeChannelSnapshot(IEnumerable<ChannelInfo> enabledChannels)
    {
        var channels = Enumerable.Range(0, ChannelHardwareProfile.ChannelCapacity)
            .Select(channelId =>
            {
                var channel = ChannelHardwareProfile.CreateDefaultChannel(channelId);
                channel.IsEnabled = false;
                return channel;
            })
            .ToDictionary(channel => channel.ChannelId, channel => channel);

        foreach (var channel in enabledChannels.Where(channel => channel.IsEnabled))
        {
            channels[channel.ChannelId] = channel;
        }

        return channels.Values
            .OrderBy(channel => channel.ChannelId)
            .ToArray();
    }

    private async Task<ProjectMeta> ReadMetaAsync(string projectRoot)
    {
        await _projectMetadataLock.WaitAsync();
        try
        {
            return await ReadMetaUnsafeAsync(projectRoot);
        }
        finally
        {
            _projectMetadataLock.Release();
        }
    }

    private async Task WriteMetaAsync(string projectRoot, ProjectMeta meta)
    {
        await _projectMetadataLock.WaitAsync();
        try
        {
            await WriteMetaUnsafeAsync(projectRoot, meta);
        }
        finally
        {
            _projectMetadataLock.Release();
        }
    }

    private async Task<ProjectMeta> ReadMetaUnsafeAsync(string projectRoot)
    {
        var metadataFile = ProjectStructure.MetadataFile(projectRoot);
        if (!File.Exists(metadataFile))
        {
            throw new FileNotFoundException("project.json was not found.", metadataFile);
        }

        var json = await File.ReadAllTextAsync(metadataFile);
        using var document = JsonDocument.Parse(json);

        var meta = JsonSerializer.Deserialize<ProjectMeta>(json, JsonOptions)
                   ?? throw new InvalidDataException("project.json is invalid.");

        var needsRewrite = false;
        if (string.IsNullOrWhiteSpace(meta.Path) ||
            !string.Equals(Path.GetFullPath(meta.Path), Path.GetFullPath(projectRoot), StringComparison.OrdinalIgnoreCase))
        {
            meta.Path = Path.GetFullPath(projectRoot);
            needsRewrite = true;
        }

        if (string.IsNullOrWhiteSpace(meta.Version))
        {
            meta.Version = "2.0";
            needsRewrite = true;
        }

        if (!document.RootElement.TryGetProperty(nameof(ProjectMeta.ExperimentIds), out _))
        {
            var experimentsRoot = ProjectStructure.ExperimentsRoot(projectRoot);
            if (Directory.Exists(experimentsRoot))
            {
                meta.ExperimentIds = Directory.GetDirectories(experimentsRoot)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                needsRewrite = true;
            }
        }

        if (needsRewrite)
        {
            await WriteMetaUnsafeAsync(projectRoot, meta);
        }

        return meta;
    }

    private async Task WriteMetaUnsafeAsync(string projectRoot, ProjectMeta meta)
    {
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(
            ProjectStructure.MetadataFile(projectRoot),
            JsonSerializer.Serialize(meta, JsonOptions));
    }

    private async Task<ExperimentInfo?> TryReadExperimentAsync(string projectRoot, string experimentId)
    {
        var experimentRoot = ProjectStructure.ExperimentRoot(projectRoot, experimentId);
        if (!Directory.Exists(experimentRoot))
        {
            return null;
        }

        var meta = await ReadMetaAsync(projectRoot);
        if (!meta.ExperimentIds.Contains(experimentId, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return await ReadExperimentByPathAsync(experimentRoot);
    }

    private async Task<ExperimentInfo> ReadExperimentByPathAsync(string experimentPath)
    {
        var metadataFile = ProjectStructure.ExperimentMetadataFile(experimentPath);
        if (!File.Exists(metadataFile))
        {
            throw new FileNotFoundException("experiment.json was not found.", metadataFile);
        }

        var experiment = JsonSerializer.Deserialize<ExperimentInfo>(
                             await File.ReadAllTextAsync(metadataFile),
                             JsonOptions)
                         ?? throw new InvalidDataException("experiment.json is invalid.");

        var expectedId = Path.GetFileName(Path.GetFullPath(experimentPath));
        var needsRewrite = false;

        if (string.IsNullOrWhiteSpace(experiment.Id))
        {
            experiment.Id = expectedId;
            needsRewrite = true;
        }

        if (string.IsNullOrWhiteSpace(experiment.Name))
        {
            experiment.Name = experiment.Id;
            needsRewrite = true;
        }

        if (string.IsNullOrWhiteSpace(experiment.Path) ||
            !string.Equals(Path.GetFullPath(experiment.Path), Path.GetFullPath(experimentPath), StringComparison.OrdinalIgnoreCase))
        {
            experiment.Path = Path.GetFullPath(experimentPath);
            needsRewrite = true;
        }

        if (needsRewrite)
        {
            await WriteExperimentAsync(experimentPath, experiment);
        }

        return experiment;
    }

    private async Task WriteExperimentAsync(string experimentPath, ExperimentInfo experiment)
    {
        Directory.CreateDirectory(experimentPath);
        await File.WriteAllTextAsync(
            ProjectStructure.ExperimentMetadataFile(experimentPath),
            JsonSerializer.Serialize(experiment, JsonOptions));
    }

    private static ProjectFileEntry BuildDirectoryEntry(string path)
    {
        var entry = new ProjectFileEntry
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = true
        };

        if (!Directory.Exists(path))
        {
            return entry;
        }

        foreach (var directory in Directory.GetDirectories(path).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            entry.Children.Add(BuildDirectoryEntry(directory));
        }

        foreach (var file in Directory.GetFiles(path).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            entry.Children.Add(new ProjectFileEntry
            {
                Name = Path.GetFileName(file),
                FullPath = file,
                IsDirectory = false
            });
        }

        return entry;
    }

    private void PublishProjectChanged(ProjectMeta? project, ExperimentInfo? experiment, ProjectChangeKind changeKind) =>
        _messenger.Send(new ProjectChangedMessage(project, experiment, changeKind));

    private static bool ProjectExists(string projectRoot) =>
        Directory.Exists(projectRoot) && File.Exists(ProjectStructure.MetadataFile(projectRoot));

    private static string ValidateBasePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Select a project root path first.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path.Trim());
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Project root path does not exist: {fullPath}");
        }

        return fullPath;
    }

    private static string ValidateName(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} cannot be empty.", label);
        }

        var trimmed = value.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"{label} contains invalid characters.", label);
        }

        return trimmed;
    }

    private static void EnsureUnderRoot(string childPath, string rootPath)
    {
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedChild = childPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!normalizedChild.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected experiment must be inside the current project's Experiments folder.");
        }
    }
}
