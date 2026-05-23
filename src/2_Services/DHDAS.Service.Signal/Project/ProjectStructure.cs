namespace DHDAS.Service.Signal.Project;

internal static class ProjectStructure
{
    public const string ConfigFolderName = "Config";
    public const string ExperimentsFolderName = "Experiments";
    public const string RawDataFolderName = "RawData";
    public const string ResultsFolderName = "Results";

    public static readonly string[] RequiredFolders =
    {
        ConfigFolderName,
        ExperimentsFolderName
    };

    public static string MetadataFile(string projectRoot) => Path.Combine(projectRoot, "project.json");
    public static string ProjectConfigFolder(string projectRoot) => Path.Combine(projectRoot, ConfigFolderName);
    public static string ProjectChannelsFile(string projectRoot) => Path.Combine(ProjectConfigFolder(projectRoot), "channels.json");
    public static string ExperimentsRoot(string projectRoot) => Path.Combine(projectRoot, ExperimentsFolderName);
    public static string ExperimentRoot(string projectRoot, string experimentId) => Path.Combine(ExperimentsRoot(projectRoot), experimentId);
    public static string ExperimentMetadataFile(string experimentRoot) => Path.Combine(experimentRoot, "experiment.json");
    public static string ExperimentConfigFolder(string experimentRoot) => Path.Combine(experimentRoot, ConfigFolderName);
    public static string ExperimentChannelsFile(string experimentRoot) => Path.Combine(ExperimentConfigFolder(experimentRoot), "channels.json");
    public static string ExperimentRawDataFolder(string experimentRoot) => Path.Combine(experimentRoot, RawDataFolderName);
    public static string ExperimentResultsFolder(string experimentRoot) => Path.Combine(experimentRoot, ResultsFolderName);
    public static string RecordedDataFile(string experimentRoot) => Path.Combine(ExperimentRawDataFolder(experimentRoot), "recorded_data.dat");
}
