using System.Threading.Channels;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Models;

namespace DHDAS.Contracts.Services;

public interface IPipelineNode
{
    string NodeId { get; }

    // 允许为空：如果为空，说明此节点是起点
    void SetInput(ChannelReader<RefCountBuffer<RawDataPacket>>? reader);

    // 允许为空：如果为空，说明此节点是终点
    void SetOutput(ChannelWriter<RefCountBuffer<RawDataPacket>>? writer);
}