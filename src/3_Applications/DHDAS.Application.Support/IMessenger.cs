namespace DHDAS.Application.Support;

/// <summary>
/// 消息中转站接口
/// </summary>
public interface IMessenger
{
    void Send<T>(T message);
}

/// <summary>
/// 消息中转站的简单实现（Demo期使用）
/// </summary>
public class AppMessenger : IMessenger
{
    public void Send<T>(T message)
    {
        // 目前仅打印到控制台，后续可改为真正的 Reactive 消息分发
        System.Console.WriteLine($"[消息总线] 发送消息类型: {typeof(T).Name}");
    }
}