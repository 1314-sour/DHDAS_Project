using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Services;

public interface IDistributedFeedbackService
{
    IDisposable Subscribe(Action<DistributedFeedbackMessage> handler);

    void Publish(string title, string message, string level = "Info");
}
