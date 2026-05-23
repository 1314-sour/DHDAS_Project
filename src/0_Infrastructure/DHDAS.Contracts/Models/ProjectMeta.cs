namespace DHDAS.Contracts.Models;

public class ProjectMeta
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "2.0";
    public string? LastOpenedExperiment { get; set; }
    public string? LastActiveExperimentId { get; set; }
    public List<string> ExperimentIds { get; set; } = new();
}
