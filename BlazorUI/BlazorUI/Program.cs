using BlazorUI.Client.Components.Pages;
using BlazorUI.Components;
using BlazorUI.Components.Pages;
using BlazorUI.Hubs;
using BlazorUI.Shared;
using Emgu.CV;
using LiteDB;
using VideoProcessing;
using JsonSerializer = System.Text.Json.JsonSerializer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSignalR().AddMessagePackProtocol();

var database = new LiteDatabase("./motion-detection.db");
var historyCol = database.GetCollection<MotionHistory>();
var streamsCol = database.GetCollection<StreamConfig>();
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
        return VideoProcessor.StreamMotionDetectionCUDA(capture, stream, new CancellationToken(),
            collection: historyCol);
    });
}

var app = builder.Build();
app.MapHub<HistoryServerHub>("/historyhub");

app.MapGet("/history/{stream}/{history}", (string stream, int history) =>
{
    var oldest = DateTime.UtcNow - TimeSpan.FromMinutes(history);
    var data = historyCol
        .Find(x => x.StreamName.Equals(stream, StringComparison.InvariantCultureIgnoreCase))
        .Where(x => x.MotionTime.ToUniversalTime() > oldest.ToUniversalTime())
        .OrderBy(x => x.MotionTime).ToArray();

    return Results.Text(JsonSerializer.Serialize(new MotionHistoryRequest(){History = data}));
});
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