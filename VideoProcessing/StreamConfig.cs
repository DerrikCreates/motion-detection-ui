using System.ComponentModel.DataAnnotations;

namespace VideoProcessing;

public class StreamConfig
{
    [Required] public string StreamName { get; set; }
    [Required] public string StreamUrl { get; set; } = "rtsp://100.125.94.97:8554/gerbil-top";
    [Required] public int FramesToSkip { get; set; } = 60;
    [Required] public int MOGHistory { get; set; } = 100;

    [Required] public int MOGThreshold { get; set; } = 100;
    [Required] public int ThresholdMin { get; set; } = 220;

    /// <summary>
    /// how much to scale the image down by before processing
    /// defaults to dividing the image by 4
    /// </summary>
    public float ImageScale { get; set; } = 4;
    public int ThresholdMax { get; set; } = 255;
}