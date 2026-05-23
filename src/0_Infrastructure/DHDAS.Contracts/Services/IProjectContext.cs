namespace DHDAS.Contracts.Services;

public interface IProjectContext
{
    string? CurrentProjectRoot { get; }
    string? CurrentProjectName { get; }
    IReadOnlyList<(string Name, string Root)> RecentProjects { get; }
    void SetCurrentProject(string name, string root);
    void UpdateProjectEntry(string name, string root);
    void RemoveProject(string root);
    void ClearCurrentProject();
    event Action? ProjectChanged;
}
