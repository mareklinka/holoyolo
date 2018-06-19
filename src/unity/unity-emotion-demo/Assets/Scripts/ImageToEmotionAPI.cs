using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using HoloLensCameraStream;
using Debug = UnityEngine.Debug;

public class ImageToEmotionAPI : MonoBehaviour
{
    private VideoCapture capture;

    public string LatestFrame { get; private set; }

    private bool captureStarted;

    private int iterations = 0;
    private TimeSpan duration = TimeSpan.Zero;

    void Start()
    {
        VideoCapture.CreateAync(OnCreatedCallback);
    }

    private void OnCreatedCallback(VideoCapture captureobject)
    {
        capture = captureobject;

        capture.StartVideoModeAsync(
            new CameraParameters(cameraResolutionWidth: 640, cameraResolutionHeight: 480, pixelFormat: CapturePixelFormat.NV12), OnVideoModeStartedCallback);
    }

    private void OnVideoModeStartedCallback(VideoCaptureResult result)
    {
        captureStarted = true;

        Task.Run(() => ProcessCamera());
    }

    private void ProcessCamera()
    {
        while (true)
        {
            capture.RequestNextFrameSample(OnFrameSampleAcquired);
        }
    }

    private void OnFrameSampleAcquired(VideoCaptureSample videocapturesample)
    {
        if (videocapturesample == null)
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        var data = new byte[videocapturesample.dataLength];
        videocapturesample.CopyRawImageDataIntoBuffer(data);

        DoStuffWithData(data);

        sw.Stop();
        duration += sw.Elapsed;
        ++iterations;

        if (iterations % 100 == 0)
        {
            Debug.Log("Average time per frame " + duration.TotalMilliseconds / iterations + "ms");
        }
    }

    private void DoStuffWithData(byte[] data)
    {
        Debug.Log("Received framebuffer of length " + data.Length);

        LatestFrame = ByteArrayToHexViaLookup32(data);
    }

    private static readonly uint[] _lookup32 = CreateLookup32();

    private static uint[] CreateLookup32()
    {
        var result = new uint[256];
        for (int i = 0; i < 256; i++)
        {
            string s = i.ToString("X2");
            result[i] = ((uint)s[0]) + ((uint)s[1] << 16);
        }
        return result;
    }

    private static string ByteArrayToHexViaLookup32(byte[] bytes)
    {
        var lookup32 = _lookup32;
        var result = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            var val = lookup32[bytes[i]];
            result[2 * i] = (char)val;
            result[2 * i + 1] = (char)(val >> 16);
        }
        return new string(result);
    }
}
