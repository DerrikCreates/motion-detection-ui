using BlazorUI.Hubs;

namespace BlazorUI.Client.Hubs;

public class HistoryClientHub : IHistoryClientHub
{
    public Task OnDataSince(MotionHistory[] History)
    {
        Console.WriteLine($"Client on data since {History.Length}");
        return Task.CompletedTask;
    }
}