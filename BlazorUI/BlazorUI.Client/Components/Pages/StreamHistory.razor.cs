using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Timers;
using System.Web;
using Blazored.Video;
using Blazored.Video.Support;
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
    protected string _streamName ="gerbil-top";
    protected HubConnection _connection;
    protected IHistoryServerHub _hubProxy;
    protected string _videoPlaybackUrl;
    protected BlazorPlot Plot { get; set; }
    Timer _timer = new();
    private List<double> data = new();
    [Inject] private NavigationManager NavigationManager { get; set; }
    public BlazoredVideo VideoPlayer { get; set; }



    protected override async Task OnInitializedAsync()
    {
        base.OnInitialized();

        return;
        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5174/historyhub")
            .AddMessagePackProtocol()
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
                            Console.WriteLine($"local {clickTime.ToLocalTime()}, utc, {clickTime.ToUniversalTime()}");
                            _videoPlaybackUrl = "";
                            StateHasChanged();
                            var videoSrc  = MediaMtxHelpers.MediaMtxPlaybackUrl("100.125.94.97:9996",
                                "gerbil-top", clickTime, 60);

                            VideoPlayer.SetSrcAsync(videoSrc);
                            
                        }

                        StateHasChanged();
                    });

            Plot.UserInputProcessor.UserActionResponses.Add(clickResponse);
        }
    }

    protected async Task RefreshData()
    {
        var start = DateTime.UtcNow - TimeSpan.FromMinutes(History);
        Console.WriteLine($"Getting data starting from {start} to now:{DateTime.UtcNow}");
        //_hubProxy.GetDataSince("gerbil-top", start);

        HttpClient client = new();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get,
            NavigationManager.BaseUri + $"history/{_streamName}/{History}"));

        if (!response.IsSuccessStatusCode)
        {
            return;
        }
        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<MotionHistoryRequest>(json);
        if (data is null)
        {
            return;
        }
        UpdateGraph(data.History);
    }

    protected void OnPlotClick(MouseEventArgs m)
    {
        var pixel = new Pixel(m.OffsetX, m.OffsetY);
        var p = Plot.Plot.GetCoordinates(pixel);
        Console.WriteLine(p);
    }

    public Task OnDataSince(List<MotionHistory> history)
    {
        //var history = JsonSerializer.Deserialize<History>(historyJson);
        Stopwatch sw = new();
        sw.Start();
        Console.WriteLine($"OnDataSince:: {history.Count}");

        var data = history.Select(x => x.MotionAmount).ToArray();
        var time = history.Select(x => x.MotionTime.ToLocalTime()).ToArray();

        Plot.Plot.Clear();

        Plot.Plot.Add.SignalXY(time, data);
        //Plot.Plot.Add.Scatter(time, data);
        Plot.Plot.Axes.DateTimeTicksBottom();
        Plot.Refresh();
        sw.Stop();
        Console.WriteLine(sw.ElapsedMilliseconds);

        return Task.CompletedTask;
    }

    protected void UpdateGraph(MotionHistory[] history)
    {
        
        //var history = JsonSerializer.Deserialize<History>(historyJson);
        Stopwatch sw = new();
        sw.Start();
        Console.WriteLine($"OnDataSince:: {history.Length}");

        var data = history.Select(x => x.MotionAmount).ToArray();
        var time = history.Select(x => x.MotionTime.ToLocalTime()).ToArray();

        Plot.Plot.Clear();

        Plot.Plot.Add.SignalXY(time, data);
        //Plot.Plot.Add.Scatter(time, data);
        Plot.Plot.Axes.DateTimeTicksBottom();
        Plot.Plot.Axes.AutoScale();
        Plot.Refresh();
        sw.Stop();
        Console.WriteLine(sw.ElapsedMilliseconds);

    }
}