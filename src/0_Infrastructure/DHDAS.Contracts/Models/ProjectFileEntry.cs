namespace DHDAS.Contracts.Models;

public class ProjectFileEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public List<ProjectFileEntry> Children { get; set; } = new();
}
