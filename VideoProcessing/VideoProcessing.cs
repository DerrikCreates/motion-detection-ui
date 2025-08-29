using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.Cuda;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using LiteDB;

public class StreamFeedback
{
    public double Mean { get; set; }
    public DateTime Time { get; set; }
    public TimeSpan FrameProcessTime { get; set; }
    public byte[] DebugImage { get; set; }
}

public class MotionHistory
{
    public string StreamName { get; set; }
    public DateTime MotionTime { get; set; }
    public double MotionAmount { get; set; }
}

public class VideoProcessing
{
    private static LiteDatabase motionDB = new LiteDatabase("./motion-history.db");

    // this feels gross but i dont have a good explanation as to why so fuck it
    public static ConcurrentDictionary<string, StreamFeedback> Feedback = new();
    public static ConcurrentDictionary<string, CancellationToken> Streams = new();


    //string url = "http://localhost:8888/test/index.m3u8";


    /// <summary>
    /// 
    /// </summary>
    /// <param name="capture"></param>
    /// <param name="skipFrames"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="mogHistory">MOG2 frame history</param>
    /// <param name="mogThreshold">MOG2 threshold</param>
    /// <param name="threshMin">CV threshold min</param>
    /// <param name="threshMax">CV threshold max</param>
    /// <param name="areaSize"> min size of contour area for it to be considered motion</param>
    public static async Task StreamMotionDetection(VideoCapture capture, int skipFrames,
        CancellationToken cancellationToken, int mogHistory = 20, int mogThreshold = 100, int threshMin = 220,
        int threshMax = 255, int areaSize = 40)
    {
        Stopwatch sw = new();
        using var subtractor = new BackgroundSubtractorMOG2(history: mogHistory, varThreshold: mogThreshold);
        using Mat currentFrame = new();
        using Mat mask = new();
        using Mat threshold = new();
        using var contours = new VectorOfVectorOfPoint();


        // using  var capture = new VideoCapture("V:\\_projects\\Lumi\\celestialcrittercams\\shorts\\pluto-zoomies.mov");

        int lastProcess = 0;
        while (true)
        {
            Console.WriteLine("--Loop--");
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (capture.IsOpened == false)
            {
                Console.WriteLine("failed to open stream");
                return;
            }

            sw.Restart();
            if (capture.Grab() && lastProcess > skipFrames)
            {
                capture.Retrieve(currentFrame);
                lastProcess = 0;
                Console.WriteLine("Processing frame");
            }
            else
            {
                lastProcess++;
                Console.WriteLine($"Skipping frame frames skipped: {lastProcess}");
                continue;
            }

            if (currentFrame.IsEmpty)
            {
                continue;
            }

            subtractor.Apply(currentFrame, mask);
            contours.Clear();
            CvInvoke.Threshold(mask, threshold, threshMin, threshMax, ThresholdType.Binary);

            CvInvoke.FindContours(threshold, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

            int largeAreas = 0;
            for (int i = 0; i < contours.Size; i++)
            {
                var contour = contours[i];

                if (CvInvoke.ContourArea(contour) > areaSize)
                {
                    largeAreas++;
                    //  var rect = CvInvoke.BoundingRectangle(contours[i]);
                    // CvInvoke.Rectangle(currentFrame, rect, new MCvScalar(0, 0, 255), thickness: 3);
                }
            }

            sw.Stop();
            Console.WriteLine($"Process Time:{sw.ElapsedMilliseconds} Motion Areas: {largeAreas}");
        }
    }

    
    //TODO: make this take the StreamConfig type for params since im already using it for the db
    public static async Task StreamMotionDetectionCUDA(VideoCapture capture, string streamName, int skipFrames,
        CancellationToken cancellationToken, int mogHistory = 100, int mogThreshold = 100, int threshMin = 100,
        int threshMax = 255, ILiteCollection<MotionHistory>? collection = null)
    {
        Console.WriteLine("Starting CUDA motion detection");
        if (Streams.TryGetValue(streamName, out var value))
        {
            Console.WriteLine($"motion detection stream {streamName} already exists returning");
            // this stream already exists
            // maybe destroy?
            return;
        }
        else
        {
            Streams.TryAdd(streamName, cancellationToken);
        }

        Stopwatch sw = new();
        using var subtractor = new CudaBackgroundSubtractorMOG2(history: mogHistory, varThreshold: mogThreshold);


        using Mat currentFrame = new();
        using Mat mask = new();
        using Mat threshold = new();
        using Mat debug = new();
        using Mat currentFrameSmall = new();
        using var contours = new VectorOfVectorOfPoint();


        using CudaImage<Bgr, byte> cudaFrame = new();
        using CudaImage<Bgr, byte> cudaMask = new();
        using CudaImage<Bgr, byte> cudaThresh = new();
        using CudaImage<Bgr, byte> cudaFrameSmall = new();
        using Mat color = new();

        var startTime = DateTime.UtcNow;

        // using  var capture = new VideoCapture("V:\\_projects\\Lumi\\celestialcrittercams\\shorts\\pluto-zoomies.mov");

        int lastProcess = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Streams.TryRemove(streamName, out _);
                Console.WriteLine($"motion detection stream: {streamName} has been cancelled");
                return;
            }

            if (capture.IsOpened == false)
            {
                Console.WriteLine("failed to open stream");
                return;
            }

            sw.Restart();
            if (capture.Grab())
            {
                if (lastProcess > skipFrames)
                {
                    capture.Retrieve(currentFrame);
                    lastProcess = 0;
                }
                else
                {
                    lastProcess++;
                    continue;
                }
            }
            else
            {
                Console.WriteLine("no frames to grab");
            }

            if (currentFrame.IsEmpty)
            {
                continue;
            }

            startTime = DateTime.UtcNow;


            cudaFrame.Upload(currentFrame);

            //CudaInvoke.CvtColor(cudaFrame, cudaGray, ColorConversion.Bgr2Gray);
            Size size = new(currentFrame.Width / 2, currentFrame.Height / 2);
            CudaInvoke.Resize(cudaFrame, cudaFrameSmall, size);
            subtractor.Apply(cudaFrameSmall, cudaMask);
            contours.Clear();
            CudaInvoke.Threshold(cudaMask, cudaThresh, threshMin, threshMax, ThresholdType.Binary);
            cudaThresh.Download(threshold);
            cudaFrameSmall.Download(currentFrameSmall);

            CvInvoke.CvtColor(threshold, color, ColorConversion.Gray2Bgr);
            CvInvoke.Add(currentFrameSmall, color, debug);
            var image = CvInvoke.Imencode(".jpg", debug);
            var mean = CvInvoke.Mean(threshold);

            collection?.Insert(new MotionHistory()
            {
                MotionAmount = mean.V0, MotionTime = DateTime.UtcNow, StreamName = streamName
            });


            sw.Stop();
            if (Feedback.TryGetValue(streamName, out var feedback))
            {
                feedback.Time = startTime;
                feedback.Mean = mean.V0;
                feedback.DebugImage = image;
                feedback.FrameProcessTime = sw.Elapsed;
            }
            else
            {
                var newFB = new StreamFeedback();
                newFB.Time = startTime;
                newFB.Mean = mean.V0;
                newFB.DebugImage = image;
                newFB.FrameProcessTime = sw.Elapsed;

                Feedback.TryAdd(streamName, newFB);
            }


            /*
            CvInvoke.FindContours(threshold, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

            int largeAreas = 0;

            for (int i = 0; i < contours.Size; i++)
            {
                var contour = contours[i];

                if (CvInvoke.ContourArea(contour) > areaSize)
                {
                    largeAreas++;
                    //var rect = CvInvoke.BoundingRectangle(contours[i]);
                    //CvInvoke.Rectangle(currentFrame, rect, new MCvScalar(0, 0, 255), thickness: 3);
                }
            }
            */
        }
    }


    void notes()
    {
        //FFmpegLoader.FFmpegPath = "V:\\_projects\\VideoManagement\\VideoManagement\\ffmpeg";
// Load the YOLO predictor
        var capture = new VideoCapture("srt://100.125.94.97:8890?streamid=read:test", VideoCapture.API.Ffmpeg);
        var frame = new Mat();
        var prevFrame = new Mat();
        Mat gray = new Mat();
        Mat diff = new Mat();
        Mat thresh = new Mat();

        int count = 0;
        int toSkip = 30;

        int motionThreshold = 15000;
        Stopwatch sw = new();
        Mat mask = new Mat(frame.Size, DepthType.Cv8U, 1);
        mask.SetTo(new MCvScalar(0));

// Draw your shape on the mask
        Point[] polygon = new Point[]
        {
            new Point(100, 100),
            new Point(150, 80),
            new Point(200, 120),
            new Point(1080, 1060)
        };
        VectorOfPoint p = new VectorOfPoint(polygon);
        CvInvoke.FillConvexPoly(mask, p, new MCvScalar(255));

// Apply mask to grayscale frame
        Mat maskedGray = new Mat();
        CvInvoke.BitwiseAnd(gray, gray, maskedGray, mask);
        gray = maskedGray;

        while (true)
        {
            if (count < 15)
            {
                capture.Grab();
                count++;
                Console.WriteLine("Skip");
                continue;
            }

            sw.Reset();
            sw.Start();
            Console.WriteLine("read");
            capture.Read(frame);
            if (frame.IsEmpty) break;

            CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);

            if (!prevFrame.IsEmpty)
            {
                CvInvoke.AbsDiff(gray, prevFrame, diff);
                CvInvoke.Threshold(diff, thresh, 25, 255, ThresholdType.Binary);

                int motionPixels = CvInvoke.CountNonZero(thresh);
                Console.WriteLine($"Motion Pixels: {motionPixels}");

                if (motionPixels > motionThreshold)
                {
                    Console.WriteLine("Significant motion detected!");
                    // Trigger alert, save frame, etc.


                    // Find contours
                    using (var contours = new VectorOfVectorOfPoint())
                    {
                        CvInvoke.FindContours(thresh, contours, null, RetrType.External,
                            ChainApproxMethod.ChainApproxSimple);

                        for (int i = 0; i < contours.Size; i++)
                        {
                            var area = CvInvoke.ContourArea(contours[i]);
                            var center = CvInvoke.MinEnclosingCircle(contours[i]).Center;
                            if (CvInvoke.PointPolygonTest(p, center, false) < 0)
                            {
                                //    continue;
                            }

                            if (area > 100)
                            {
                                var rect = CvInvoke.BoundingRectangle(contours[i]);
                                CvInvoke.Rectangle(frame, rect, new MCvScalar(0, 0, 255), 2); // Red box    
                            }
                        }
                    }
                }

                CvInvoke.Polylines(frame, p, true, new MCvScalar(255, 0, 0), 2);
                CvInvoke.NamedWindow("Motion", WindowFlags.FreeRatio);
                CvInvoke.Imshow("Motion", frame);
            }

            gray.CopyTo(prevFrame);
            Console.WriteLine(sw.ElapsedMilliseconds);


            if (CvInvoke.WaitKey(30) >= 0) break;
        }
    }
}