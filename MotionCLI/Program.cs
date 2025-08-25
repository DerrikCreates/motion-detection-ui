// See https://aka.ms/new-console-template for more information

using CommandLine;
using Emgu.CV;
using MotionCLI;


Parser.Default.ParseArguments<Options>(args).WithParsed(OnParsed);

void OnParsed(Options options)
{
    CancellationToken ctx = new();
    var capture = new VideoCapture(options.VideoPath, VideoCapture.API.Ffmpeg);

    if (options.UseGPU)
    {
        
        Task.Run(()=>VideoProcessing.StreamMotionDetectionCUDA(capture,"test", options.FramesToSkip, ctx));
    }
    else
    {
        
        Task.Run(()=>VideoProcessing.StreamMotionDetection(capture, options.FramesToSkip, ctx));
    }
}


await Task.Delay(-1);