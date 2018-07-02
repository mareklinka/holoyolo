using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using HoloLensCameraStream;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class ImageToEmotionAPI : MonoBehaviour
{
    private VideoCapture capture;
    private int width = 640;
    private int height = 480;

    void Start()
    {
        VideoCapture.CreateAync(OnCreatedCallback);
    }

    private void OnCreatedCallback(VideoCapture captureobject)
    {
        capture = captureobject;

        capture.StartVideoModeAsync(
            new CameraParameters(cameraResolutionWidth: 320, cameraResolutionHeight: 240, pixelFormat: CapturePixelFormat.BGRA32), OnVideoModeStartedCallback);
    }

    private void OnVideoModeStartedCallback(VideoCaptureResult result)
    {
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
        
        var data = new byte[videocapturesample.dataLength];
        videocapturesample.CopyRawImageDataIntoBuffer(data);

        DoStuffWithData(data);
    }

    private void DoStuffWithData(byte[] data)
    {
        Debug.Log("Received framebuffer of length " + data.Length);

        var payload = ByteArrayToHexViaLookup32(data);

        UnityEngine.WSA.Application.InvokeOnAppThread(() => StartCoroutine(PostImage(payload)), true);
    }

    private IEnumerator PostImage(string data)
    {
        var content = JsonUtility.ToJson(new DetectionRequest
        {
            data = data,
            width = 320,
            height = 240
        });

        var request = new UnityWebRequest("http://10.201.91.94:5000/image", "POST")
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(content)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json");

        var response = request.SendWebRequest();

        yield return response;

        //var responseContent = JsonUtility.FromJson<DetectionResponse>(response.webRequest.downloadHandler.text);

        Debug.Log("Bruh, we are done");
    }

    [Serializable]
    public class DetectionResponse
    {
    }

    [Serializable]
    public class DetectionRequest
    {
        public string data;

        public int width;

        public int height;
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
