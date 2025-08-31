using System.Timers;
using BlazorUI.Client.Hubs;
using BlazorUI.Hubs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using ScottPlot;
using ScottPlot.Blazor;
using TypedSignalR.Client;
using Timer = System.Timers.Timer;

namespace BlazorUI.Client.Components.Pages;

public class StreamHistoryBase : ComponentBase
{
    protected HubConnection _connection;
    protected HistoryClientHub _historyClient = new();
    protected BlazorPlot plot;
    Timer _timer = new();
    private List<double> data = new();
    [Parameter, SupplyParameterFromQuery] public string? StreamName { get; set; }
    [Parameter, SupplyParameterFromQuery] public long? ViewSize { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _connection = new HubConnectionBuilder()
            .WithUrl("/historyhub")
            .Build();


        _connection.Register<IHistoryClientHub>(_historyClient);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            if (StreamName is null)
            {
                Console.WriteLine("Missing stream name");
                return;
            }


            plot.Plot.Add.Signal(Generate.Sin());
            _timer.Elapsed += OnUpdateTimer;
            _timer.Interval = 2000;
        }
    }

    private void OnUpdateTimer(object? sender, ElapsedEventArgs e)
    {
    }

    protected void test()
    {
        Console.WriteLine($"interactive:{RendererInfo.IsInteractive}, name:{RendererInfo.Name}");
    }
}