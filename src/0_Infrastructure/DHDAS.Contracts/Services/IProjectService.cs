using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Services;

public interface IProjectService
{
    Task<ProjectMeta> CreateProject(string name, string path, string author = "", string description = "");
    Task<ProjectMeta> OpenProject(string path);
    Task CloseProject();
    Task UpdateProject(string projectRoot, string name, string description);
    Task DeleteProject(string projectRoot, bool deleteFiles = false);
    Task<ProjectMeta?> GetProjectMeta(string projectRoot);
    Task SaveLastOpenedExperiment(string projectRoot, string experimentId);

    Task<List<ExperimentInfo>> GetExperiments(string projectRoot);
    Task<ExperimentInfo> CreateExperiment(string projectRoot, string experimentName);
    Task<ExperimentInfo> ImportExperiment(string projectRoot, string experimentFolderPath);
    Task<ExperimentInfo?> ActivateExperiment(string projectRoot, string experimentId);
    Task DeleteExperiment(string projectRoot, string experimentId, bool deleteFiles = false);
    Task SaveExperimentConfig(string experimentPath, ExperimentConfig config);
    Task SaveChannelConfig(string projectRoot, IReadOnlyList<ChannelInfo> channels);
    Task<IReadOnlyList<ChannelInfo>> LoadChannelConfig(string projectRoot);
    Task<IReadOnlyList<ProjectFileEntry>> GetExperimentDirectoryTree(string experimentPath);
}
