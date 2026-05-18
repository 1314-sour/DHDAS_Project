namespace DHDAS.Contracts.Models;

public sealed class InstrumentApplyResult
{
    public bool IsSuccess { get; init; }
    public int AppliedCount { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static InstrumentApplyResult Success(int appliedCount, string message) => new()
    {
        IsSuccess = true,
        AppliedCount = appliedCount,
        Message = message
    };

    public static InstrumentApplyResult Failure(IReadOnlyList<string> errors) => new()
    {
        IsSuccess = false,
        Message = errors.Count == 0 ? "通道配置校验失败" : errors[0],
        Errors = errors
    };
}
