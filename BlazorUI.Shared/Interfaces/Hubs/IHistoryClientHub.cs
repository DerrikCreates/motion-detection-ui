namespace BlazorUI.Hubs;

public interface IHistoryClientHub
{
    public Task OnDataSince(List<MotionHistory> history);
}

public class History
{
    public MotionHistory[] MotionHistory { get; set; }
}

