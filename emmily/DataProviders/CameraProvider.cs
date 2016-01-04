using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace emmily.DataProviders
{
    public class CameraProvider
    {
        private static CameraProvider _provider;

        public static CameraProvider GetInstance()
        {
            if (_provider == null)
                _provider = new CameraProvider();

            return _provider;
        }

        private MediaCapture _mediaCapture;
        private bool _isInitialized;
        private VideoEncodingProperties _previewStream;
        private FaceDetectionEffect _faceDetectionEffect;
        private bool _analyzing;

        private DateTime _lastFaceDetectedTimeStamp;
        private int _lastNumberOfFacedDetected = 0;

        public event EventHandler<PeopleDetectionEventArgs> DetectedPeopleChanged;


        private CameraProvider()
        {
            _isInitialized = false;
            _analyzing = false;
        }

        public async Task<bool> InitializeCameraAsync()
        {
            if (_mediaCapture == null)
            {
                var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (allVideoDevices.Count == 0) return false;

                var device = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && 
                             x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                var camera = device ?? allVideoDevices.First();

                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings
                {
                    VideoDeviceId = camera.Id,
                    StreamingCaptureMode = StreamingCaptureMode.Video
                };

                try
                {
                    await _mediaCapture.InitializeAsync(settings);
                    _isInitialized = true;
                    CaptureElement element = new CaptureElement();
                    element.Source = _mediaCapture;
                    await _mediaCapture.StartPreviewAsync();

                    var previewProperties = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                    _previewStream = previewProperties as VideoEncodingProperties;

                    var definition = new FaceDetectionEffectDefinition();
                    definition.SynchronousDetectionEnabled = false;
                    definition.DetectionMode = FaceDetectionMode.HighPerformance;

                    _faceDetectionEffect = (await _mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview)) as FaceDetectionEffect;
                    _faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(100);
                    _faceDetectionEffect.Enabled = true;
                    _faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;

                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    _mediaCapture = null;
                    Debug.WriteLine("The app was denied access to the camera");
                    return false;
                }
            }
            return true;
        }

        private void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            if (args.ResultFrame.DetectedFaces.Count > 0)
            {
                if ((DateTime.Now - _lastFaceDetectedTimeStamp).Seconds > 10 || args.ResultFrame.DetectedFaces.Count > _lastNumberOfFacedDetected)
                {
                    _lastFaceDetectedTimeStamp = DateTime.Now;
                    _lastNumberOfFacedDetected = args.ResultFrame.DetectedFaces.Count;

                    if (!_analyzing)
                    {

                        var frame = args.ResultFrame;
                        var box = frame.DetectedFaces.First().FaceBox;



                        if (((double)box.Height / (double)_previewStream.Height) < 0.3)
                        {
                            // Not close enough
                        }
                        else
                        {
                            if (!_analyzing && (DateTime.Now - _lastFaceDetectedTimeStamp).Seconds > 10)
                            {
                                TakePhotoAndAnalyzeAsync();
                            }
                        }

                        //Debug.WriteLine("Face Detected: Height: " + box.Height + " Width: " + box.Width);

                    }

                }

            }
            else if (!_analyzing)
            {
                Debug.WriteLine("No face detected");
            }
        }

        private async Task TakePhotoAndAnalyzeAsync()
        {
            Debug.WriteLine("Analyzing");
            // if no one is subscribed, don't waste my quota
            if (DetectedPeopleChanged == null) return;
            _analyzing = true;
            using (var stream = new InMemoryRandomAccessStream())
            {
                try
                {
                    await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
                    using (var outputStream = new InMemoryRandomAccessStream())
                    {
                        var decoder = await BitmapDecoder.CreateAsync(stream);
                        var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);
                        var properties = new BitmapPropertySet { { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) } };

                        await encoder.BitmapProperties.SetPropertiesAsync(properties);
                        await encoder.FlushAsync();

                        var faceStream = outputStream.AsStream();
                        faceStream.Seek(0, SeekOrigin.Begin);

                        var emotions = await EmotionProvider.GetInstance().IdentifyEmotionAsync(faceStream);
                        _lastFaceDetectedTimeStamp = DateTime.Now;

                        if (emotions.Count > 0)
                            DetectedPeopleChanged(this, new PeopleDetectionEventArgs()
                            {
                                Emotions = emotions
                            });


                        //MemoryStream emotionStream = new MemoryStream();
                        //await faceStream.CopyToAsync(emotionStream);

                        //faceStream.Seek(0, SeekOrigin.Begin);
                        //emotionStream.Seek(0, SeekOrigin.Begin);

                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
            _analyzing = false;
        }
    }

    public class PeopleDetectionEventArgs: EventArgs
    {
        public List<Emotion> Emotions { get; set; }
    }
}
