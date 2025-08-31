namespace BlazorUI.Hubs;

public interface IHistoryServerHub
{
    public Task GetDataSince(string StreamName, DateTime sine);
}