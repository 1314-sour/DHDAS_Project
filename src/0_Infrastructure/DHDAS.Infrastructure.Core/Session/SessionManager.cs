using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DHDAS.Infrastructure.Core.Session;

/// <summary>
/// 全局会话管理器
/// </summary>
public class SessionManager
{
    private readonly ConcurrentDictionary<int, SignalSession> _sessions = new();

    /// <summary>
    /// 获取或创建指定通道的 Session
    /// </summary>
    public SignalSession GetSession(int channelId)
    {
        // 保证在多线程环境下，同一个 channelId 只有一个 SignalSession 实例
        return _sessions.GetOrAdd(channelId, id => new SignalSession(id));
    }

    /// <summary>
    /// 获取当前所有活跃的会话
    /// </summary>
    public IEnumerable<SignalSession> GetAllSessions() => _sessions.Values;
}