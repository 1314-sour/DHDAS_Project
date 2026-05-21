using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using DHDAS.Contracts.Memory;
using DHDAS.Contracts.Models;
using DHDAS.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace DHDAS.Service.Signal;

/// <summary>
/// 管道编排器：负责根据配置方案动态连接各服务节点
/// </summary>
public class PipelineOrchestrator
{
    private readonly IEnumerable<IPipelineNode> _availableNodes;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(IEnumerable<IPipelineNode> nodes, ILogger<PipelineOrchestrator> logger)
    {
        _availableNodes = nodes;
        _logger = logger;
    }

    /// <summary>
    /// 根据定义的节点 ID 顺序构建流水线
    /// </summary>
    /// <param name="nodeSequence">节点 ID 列表，如 ["AcquisitionService", "DataPushService"]</param>
    public void BuildPipeline(List<string> nodeSequence)
    {
        foreach (var n in _availableNodes)
            Console.WriteLine($"[调试] 容器中存在的节点 ID: {n.NodeId}");

        if (nodeSequence == null || nodeSequence.Count == 0)
        {
            _logger.LogWarning("编排失败：提供的节点序列为空。");
            return;
        }

        _logger.LogInformation($"开始编排流水线，方案：{string.Join(" -> ", nodeSequence)}");

        IPipelineNode? previousNode = null;

        for (int i = 0; i < nodeSequence.Count; i++)
        {
            string nodeId = nodeSequence[i];

            // 1. 从所有已注册的节点中找到目标实例
            var currentNode = _availableNodes.FirstOrDefault(n => n.NodeId == nodeId);
            if (currentNode == null)
            {
                _logger.LogError($"编排中断：找不到 ID 为 {nodeId} 的服务节点，请检查 DI 注册。");
                throw new InvalidOperationException($"Node {nodeId} not found.");
            }

            // 2. 清除该节点旧的连线（防止重复构建导致混乱）
            currentNode.SetInput(null);
            currentNode.SetOutput(null);

            // 3. 执行连线：如果存在上一个节点，则用一根新管道连接它们
            if (previousNode != null)
            {
                // 创建高性能管道，开启同步延续以减少 400MBps 下的上下文切换开销
                var pipe = Channel.CreateBounded<RefCountBuffer<RawDataPacket>>(new BoundedChannelOptions(10000)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                    AllowSynchronousContinuations = true
                });

                // 上家的出 = 管道的入
                previousNode.SetOutput(pipe.Writer);
                // 下家的入 = 管道的出
                currentNode.SetInput(pipe.Reader);

                _logger.LogInformation($"[OK] 已建立物理连接: {previousNode.NodeId} ===> {currentNode.NodeId}");
            }

            previousNode = currentNode;
        }

        _logger.LogInformation("流水线物理编排成功。");
    }
}