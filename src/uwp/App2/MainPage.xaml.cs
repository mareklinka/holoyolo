using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.Web.Http;
using Newtonsoft.Json;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture mediaCapture;
        private readonly DisplayRequest displayRequest = new DisplayRequest();
        private MediaFrameReader mediaFrameReader;
        private readonly HttpClient http = new HttpClient();
        private readonly JsonSerializer serializer = new JsonSerializer();
        private VideoEncodingProperties previewProperties;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            displayRequest.RequestActive();

            try
            {
                var (colorSourceInfo, selectedGroup) = await MediaFrameSourceInfo();

                if (selectedGroup == null)
                {
                    return;
                }

                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
                {
                    SourceGroup = selectedGroup,
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                    MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                    StreamingCaptureMode = StreamingCaptureMode.Video
                });

                var colorFrameSource = mediaCapture.FrameSources[colorSourceInfo.Id];
                var preferredFormat = colorFrameSource.SupportedFormats.FirstOrDefault(format =>
                    format.VideoFormat.Width == 1280
                    && string.Compare(format.Subtype, MediaEncodingSubtypes.Nv12, true) == 0);

                if (preferredFormat == null)
                {
                    // Our desired format is not supported
                    return;
                }

                await colorFrameSource.SetFormatAsync(preferredFormat);
                mediaFrameReader = await mediaCapture.CreateFrameReaderAsync(colorFrameSource, MediaEncodingSubtypes.Nv12);
                
                await mediaFrameReader.StartAsync();

                Task.Run(() => ProcessPreview(mediaFrameReader));
            }
            catch (UnauthorizedAccessException)
            {
                // This will be thrown if the user denied access to the camera in privacy settings
                return;
            }

            try
            {
                previewProperties = (VideoEncodingProperties)mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                CameraPreview.Source = mediaCapture;

                await mediaCapture.StartPreviewAsync();
            }
            catch (System.IO.FileLoadException)
            {
            }
        }

        private static async Task<(MediaFrameSourceInfo colorSourceInfo, MediaFrameSourceGroup selectedGroup)> MediaFrameSourceInfo()
        {
            var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();

            if (frameSourceGroups.Count == 0)
            {
                return (null, null);
            }

            MediaFrameSourceInfo colorSourceInfo = null;
            MediaFrameSourceGroup selectedGroup = null;

            foreach (var sourceGroup in frameSourceGroups)
            {
                foreach (var sourceInfo in sourceGroup.SourceInfos)
                {
                    if (sourceInfo.MediaStreamType == MediaStreamType.VideoRecord
                        && sourceInfo.SourceKind == MediaFrameSourceKind.Color &&
                        sourceInfo.DeviceInformation?.EnclosureLocation.Panel ==
                        Windows.Devices.Enumeration.Panel.Back)
                    {
                        colorSourceInfo = sourceInfo;
                        break;
                    }
                }

                if (colorSourceInfo != null)
                {
                    selectedGroup = sourceGroup;
                    break;
                }
            }

            if (selectedGroup == null)
            {
                foreach (var sourceGroup in frameSourceGroups)
                {
                    foreach (var sourceInfo in sourceGroup.SourceInfos)
                    {
                        if (sourceInfo.MediaStreamType == MediaStreamType.VideoRecord
                            && sourceInfo.SourceKind == MediaFrameSourceKind.Color)
                        {
                            colorSourceInfo = sourceInfo;
                            break;
                        }
                    }

                    if (colorSourceInfo != null)
                    {
                        selectedGroup = sourceGroup;
                        break;
                    }
                }
            }
            
            return (colorSourceInfo, selectedGroup);
        }

        private async void ProcessPreview(MediaFrameReader reader)
        {
            while (true)
            {
                using (var frame = reader.TryAcquireLatestFrame())
                {
                    if (frame?.VideoMediaFrame == null)
                    {
                        continue;
                    }

                    var bitmap =
                        SoftwareBitmap.Convert(frame.VideoMediaFrame.SoftwareBitmap, BitmapPixelFormat.Rgba8);
                    using (bitmap)
                    {
                        const int size = 1280 * 720 * 4;
                        var data = new byte[size];

                        bitmap.CopyToBuffer(data.AsBuffer());
                        var hex = ByteArrayToHexViaLookup32(data);
                        var payload = JsonConvert.SerializeObject(new { data = hex, width = 1280, height = 720 });

                        var response = await http.PostAsync(new Uri("http://localhost:5000/image"), new HttpStringContent(payload, UnicodeEncoding.Utf8, "application/json"));

                        try
                        {
                            response.EnsureSuccessStatusCode();

                            await ParseRespone(await response.Content.ReadAsInputStreamAsync());
                        }
                        catch (Exception)
                        {
                            // todo
                        }
                    }
                }
            }
        }

        private async Task ParseRespone(IInputStream responseStream)
        {
            using (var reader = new StreamReader(responseStream.AsStreamForRead()))
            using (var json = new JsonTextReader(reader))
            {
                var response = serializer.Deserialize<Predictions>(json);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => DrawBoxes(response));
            }
        }

        private List<string> classes = new List<string>
        {
            "person",
            "bicycle",
            "car",
            "motorbike",
            "aeroplane",
            "bus",
            "train",
            "truck",
            "boat",
            "traffic light",
            "fire hydrant",
            "stop sign",
            "parking meter",
            "bench",
            "bird",
            "cat",
            "dog",
            "horse",
            "sheep",
            "cow",
            "elephant",
            "bear",
            "zebra",
            "giraffe",
            "backpack",
            "umbrella",
            "handbag",
            "tie",
            "suitcase",
            "frisbee",
            "skis",
            "snowboard",
            "sports ball",
            "kite",
            "baseball bat",
            "baseball glove",
            "skateboard",
            "surfboard",
            "tennis racket",
            "bottle",
            "wine glass",
            "cup",
            "fork",
            "knife",
            "spoon",
            "bowl",
            "banana",
            "apple",
            "sandwich",
            "orange",
            "broccoli",
            "carrot",
            "hot dog",
            "pizza",
            "donut",
            "cake",
            "chair",
            "sofa",
            "pottedplant",
            "bed",
            "diningtable",
            "toilet",
            "tvmonitor",
            "laptop",
            "mouse",
            "remote",
            "keyboard",
            "cell phone",
            "microwave",
            "oven",
            "toaster",
            "sink",
            "refrigerator",
            "book",
            "clock",
            "vase",
            "scissors",
            "teddy bear",
            "hair drier",
            "toothbrush"
        };

        private void DrawBoxes(Predictions predictions)
        {
            var ratio = CameraPreview.ActualHeight / 720;
            var previewWidth = 1280 * ratio;
            
            var previewLeft = (CameraPreview.ActualWidth - previewWidth) / 2;

            Hud.Children.Clear();

            for (var i = 0; i < predictions.Boxes.Count; i++)
            {
                var box = predictions.Boxes[i];
                var klass = predictions.Classes[i];
                var score = predictions.Scores[i];

                var rect = new Rectangle
                {
                    Width = (box.X2 - box.X1) * ratio,
                    Height = (box.Y2 - box.Y1) * ratio,
                    Stroke = new SolidColorBrush(Colors.Red),
                    StrokeThickness = 4
                };

                Canvas.SetTop(rect, box.Y1 * ratio);
                Canvas.SetLeft(rect, previewLeft + (box.X1 * ratio));

                var label = new TextBlock
                {
                    Text = $"{classes[klass]} ({Math.Round(score * 100, 2)}%)",
                    Foreground = new SolidColorBrush(Colors.Black)
                };

                var labelBorder = new Border
                {
                    Background = new SolidColorBrush(Colors.Red),
                    Child = label
                };

                labelBorder.Measure(new Size(Double.MaxValue, Double.MaxValue));

                Canvas.SetTop(labelBorder, Canvas.GetTop(rect) - labelBorder.DesiredSize.Height);
                Canvas.SetLeft(labelBorder, Canvas.GetLeft(rect) + rect.Width - labelBorder.DesiredSize.Width);

                Hud.Children.Add(rect);
                Hud.Children.Add(labelBorder);
            }
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

        private class Predictions
        {
            public List<Box> Boxes { get; set; }

            public List<float> Scores { get; set; }

            public List<int> Classes { get; set; }
        }

        private class Box
        {
            public float X1 { get; set; }

            public float Y1 { get; set; }

            public float X2 { get; set; }

            public float Y2 { get; set; }
        }
    }
}
