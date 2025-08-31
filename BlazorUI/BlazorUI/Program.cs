using BlazorUI.Client.Components.Pages;
using BlazorUI.Components;
using BlazorUI.Components.Pages;
using BlazorUI.Hubs;
using Emgu.CV;
using LiteDB;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSignalR();

var database = new LiteDatabase("./motion-detection.db");
var historyCol = database.GetCollection<MotionHistory>();
var streamsCol = database.GetCollection<Home.StreamConfig>();
builder.Services.AddSingleton(historyCol);
builder.Services.AddSingleton(streamsCol);

historyCol.EnsureIndex(x => x.StreamName);
streamsCol.EnsureIndex(x => x.StreamName);

foreach (var stream in streamsCol.FindAll())
{
    Console.WriteLine($"starting stream {stream.StreamName}");
    Task.Run(() =>
    {
        var capture = new VideoCapture(stream.StreamUrl, VideoCapture.API.Ffmpeg);
        return VideoProcessing.StreamMotionDetectionCUDA(capture, stream.StreamName, stream.FramesToSkip,
            new CancellationToken(), stream.MOGHistory, stream.MOGThreshold, stream.ThresholdMin, stream.ThresholdMax,
            historyCol);
    });
}

var app = builder.Build();
app.MapHub<HistoryServerHub>("/historyhub");
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BlazorUI.Client._Imports).Assembly);

app.Run();