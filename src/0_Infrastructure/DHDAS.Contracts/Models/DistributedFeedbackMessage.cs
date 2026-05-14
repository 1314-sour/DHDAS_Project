namespace DHDAS.Contracts.Models;

public class DistributedFeedbackMessage
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "Info";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
