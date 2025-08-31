namespace BlazorUI.Hubs;

public interface IHistoryClientHub
{
    public Task OnDataSince(MotionHistory[] History);
}