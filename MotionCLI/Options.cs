using CommandLine;

namespace MotionCLI;

public class Options
{
    [Option('v',"video",Required = true, HelpText = "Path to video/stream")]
    public string VideoPath { get; set; }
    
    [Option('s',"skip",Required = true,HelpText = "Skip every X frames")]
    public int FramesToSkip { get; set; }

    [Option('c',"cuda",Required = false)]
    public bool UseGPU { get; set; }
    
    
    
}
