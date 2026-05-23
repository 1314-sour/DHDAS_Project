namespace DHDAS.Contracts.Models;

public class ExperimentInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ExperimentConfig Config { get; set; } = new();
}
