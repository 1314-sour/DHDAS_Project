using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;

namespace DHDAS.Service.Signal.Project;

public class SignalProcessingContext : ISignalProcessingContext
{
    public ExperimentInfo? CurrentExperiment { get; private set; }
    public bool HasUnsavedChanges { get; set; }
    public Func<ExperimentInfo, Task<bool>>? OnExperimentSwitching { get; set; }
    public event Action<ExperimentInfo?>? ExperimentSwitched;

    public void NotifyExperimentSwitched(ExperimentInfo? experiment)
    {
        CurrentExperiment = experiment;
        ExperimentSwitched?.Invoke(experiment);
    }
}
