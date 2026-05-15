using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Services;

public interface INetworkService
{
    IObservable<NetworkLinkStatus> LinkStatusChanged { get; }

    IReadOnlyList<NetworkLinkStatus> GetLinkStatuses();

    void UpdateRoutingTable(List<NetworkRoute> routes);

    Task ConnectAsync(NetworkRoute route, CancellationToken ct = default);

    Task DisconnectAsync(NetworkRoute route, CancellationToken ct = default);

    void GenerateTestSinePacket(int channelId);

    void SendCurrentTestPacket();

    void SendCurrentTestPacketToRoute(NetworkRoute route);
}
