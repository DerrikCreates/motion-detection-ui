using System.Runtime.InteropServices.JavaScript;
using BlazorUI.Components.Pages;
using LiteDB;
using Microsoft.AspNetCore.SignalR;

namespace BlazorUI.Hubs;

public class HistoryServerHub : Hub<IHistoryClientHub>, IHistoryServerHub
{
    private ILiteCollection<MotionHistory> _db;

    public HistoryServerHub(ILiteCollection<MotionHistory> db)
    {
        _db = db;
    }

    public async Task GetDataSince(string streamName, DateTime since)
    {
        var data = _db
            .Find(x => x.StreamName.Equals(streamName, StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(x => x.MotionTime)
            .ToArray();
        await Clients.Caller.OnDataSince(data);
    }
}