using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Services;

public interface ISignalProcessingContext
{
    ExperimentInfo? CurrentExperiment { get; }
    bool HasUnsavedChanges { get; set; }
    Func<ExperimentInfo, Task<bool>>? OnExperimentSwitching { get; set; }
    event Action<ExperimentInfo?>? ExperimentSwitched;
    void NotifyExperimentSwitched(ExperimentInfo? experiment);
}
