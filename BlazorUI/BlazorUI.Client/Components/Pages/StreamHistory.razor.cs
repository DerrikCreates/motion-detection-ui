using System.Diagnostics;
using System.Timers;
using System.Web;
using BlazorUI.Hubs;
using BlazorUI.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using ScottPlot;
using ScottPlot.Blazor;
using ScottPlot.Plottables;
using TypedSignalR.Client;
using Timer = System.Timers.Timer;

namespace BlazorUI.Client.Components.Pages;

public class StreamHistoryBase : ComponentBase, IHistoryClientHub
{
    public int History { get; set; } = 5;
    protected string _streamName;
    protected HubConnection _connection;
    protected IHistoryServerHub _hubProxy;
    protected string _videoPlaybackUrl;
    protected BlazorPlot Plot { get; set; }
    Timer _timer = new();
    private List<double> data = new();
    [Inject] private NavigationManager NavigationManager { get; set; }


    protected override async Task OnInitializedAsync()
    {
        base.OnInitialized();


        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5174/historyhub")
            .Build();

        await _connection.StartAsync();

        _hubProxy = _connection.CreateHubProxy<IHistoryServerHub>();


        _connection.Register<IHistoryClientHub>(this);
        // _timer.Start();
    }


    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            var mouse = ScottPlot.Interactivity.StandardMouseButtons.Left;
            var clickResponse =
                new ScottPlot.Interactivity.UserActionResponses.SingleClickResponse(mouse,
                    (control, pixel) =>
                    {
                        var cord = Plot.Plot.GetCoordinates(pixel);
                        Console.WriteLine($"{pixel},{cord}");
                        var signalXy = Plot.Plot.GetPlottables<SignalXY>().FirstOrDefault();
                        if (signalXy is null)
                        {
                            return;
                        }
                        var point = signalXy.Data.GetNearestX(cord, Plot.Plot.LastRender, 100);

                        if (point.IsReal)
                        {
                            var clickTime = DateTime.FromOADate(point.X);
                           Console.WriteLine($"local { clickTime.ToLocalTime()}, utc, {clickTime.ToUniversalTime()}"); 
                            _videoPlaybackUrl = "";
                            StateHasChanged();
                            _videoPlaybackUrl = MediaMtxHelpers.MediaMtxPlaybackUrl("100.125.94.97:9996",
                                "gerbil-top", clickTime, 60);}
                            StateHasChanged();
                    });

            Plot.UserInputProcessor.UserActionResponses.Add(clickResponse);
        }
    }

    protected void RefreshData()
    {
        var start = DateTime.UtcNow - TimeSpan.FromMinutes(History);
        Console.WriteLine($"Getting data starting from {start} to now:{DateTime.UtcNow}");
        _hubProxy.GetDataSince("gerbil-top", start);
    }

    protected void OnPlotClick(MouseEventArgs m)
    {
        var pixel = new Pixel(m.OffsetX, m.OffsetY);
        var p = Plot.Plot.GetCoordinates(pixel);
        Console.WriteLine(p);
    }

    public Task OnDataSince(MotionHistory[] History)
    {
        Stopwatch sw = new();
        sw.Start();
        Console.WriteLine($"OnDataSince:: {History.Length}");

        var data = History.Select(x => x.MotionAmount).ToArray();
        var time = History.Select(x => x.MotionTime.ToLocalTime()).ToArray();

        Plot.Plot.Clear();

        Plot.Plot.Add.SignalXY(time, data);
        //Plot.Plot.Add.Scatter(time, data);
        Plot.Plot.Axes.DateTimeTicksBottom();
        Plot.Refresh();
        sw.Stop();
        Console.WriteLine(sw.ElapsedMilliseconds);

        return Task.CompletedTask;
    }
}