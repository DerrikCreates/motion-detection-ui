// See https://aka.ms/new-console-template for more information

using CommandLine;
using Emgu.CV;
using MotionCLI;
using VideoProcessing;


Parser.Default.ParseArguments<Options>(args).WithParsed(OnParsed);

void OnParsed(Options options)
{
    CancellationToken ctx = new();
    var capture = new VideoCapture(options.VideoPath, VideoCapture.API.Ffmpeg);

    if (options.UseGPU)
    {
        //Task.Run(() => VideoProcessor.StreamMotionDetectionCUDA(capture, new StreamConfig(), ctx));
    }
    else
    {
    }
}


await Task.Delay(-1);