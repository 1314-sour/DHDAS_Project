namespace DHDAS.Contracts.Models;

public enum ProjectChangeKind
{
    Opened,
    Closed,
    ExperimentChanged,
    Deleted
}

public sealed class ProjectChangedMessage
{
    public ProjectChangedMessage(ProjectMeta? project, ExperimentInfo? experiment, ProjectChangeKind kind)
    {
        Project = project;
        Experiment = experiment;
        Kind = kind;
    }

    public ProjectMeta? Project { get; }
    public ExperimentInfo? Experiment { get; }
    public ProjectChangeKind Kind { get; }
}
