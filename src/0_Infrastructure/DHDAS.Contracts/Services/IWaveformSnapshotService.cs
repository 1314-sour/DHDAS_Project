using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Services;

public interface IWaveformSnapshotService
{
    WaveformSnapshot? Latest { get; }

    IDisposable Subscribe(Action<WaveformSnapshot> handler);

    void Publish(WaveformSnapshot snapshot);
}
