using emmily.Common;
using emmily.Data;
using emmily.DataProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace emmily
{
    public sealed partial class MainPage : Page, IUserResponseConnection
    {
        public MainPage()
        {
            this.InitializeComponent();
            LoadingScreen.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        public TimeSpan TypeSpan { get; private set; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            InitClock();
            InitMusic();

            List<Task> tasks = new List<Task>();
            tasks.Add(InitWeather());
            tasks.Add(InitSpeech());
            tasks.Add(InitCamera());

            await Task.WhenAll(tasks.ToArray());
                        
            if (!_speechInit) BottomText.Text = "Speech did not initialize, but you can still look at yourself :)";
            else BottomText.Text = DefaultBottomText;

            LoadingScreen.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaElement el = sender as MediaElement;
            el.Play();
        }

        #region Clock
        private DispatcherTimer _clockTimer;
        private CustomDateProvider _dateProvider;
        private DateTime _currentDateTime;

        private void InitClock()
        {
            _dateProvider = new CustomDateProvider();
            SetTime();

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += _clockTimer_Tick;
            _clockTimer.Start();
        }

        private void _clockTimer_Tick(object sender, object e)
        {
            SetTime();
        }

        private void SetTime()
        {
            var now = DateTime.Now;
            if (_currentDateTime != null && _currentDateTime.Minute == now.Minute) return;

            _currentDateTime = now;

            var formatString = Settings.TimeFormat == TimeFormat._12 ? "h:mm tt" : "H:mm";
            Clock_Time.Text = now.ToString(formatString, CultureInfo.CurrentUICulture).ToLower();
            Clock_Date.Text = string.Format(_dateProvider, "{0}", now).ToLower();
        }

        #endregion

        #region Weather

        private async Task InitWeather()
        {
            await WeatherProvider.GetInstance().UpdateCurrentWeatherNow();
            WeatherProvider.GetInstance().WeatherRefreshed += MainPage_WeatherRefreshed;
            SetWeather();
        }

        private void MainPage_WeatherRefreshed(object sender, EventArgs e)
        {
            SetWeather();
        }

        private void SetWeather()
        {
            var currentWeather = WeatherProvider.GetInstance().CurrentData;

            if (currentWeather == null) return;

            Weather_Temp.Text = currentWeather.Temperature + currentWeather.Unit;
            Weather_High.Text = currentWeather.HighTemperature + currentWeather.Unit;
            Weather_Low.Text = currentWeather.LowTemperature + currentWeather.Unit;
            Weather_Description.Text = currentWeather.Description;
            Weather_Location.Text = currentWeather.Location.ToLower();
            Weather_Wind.Text = currentWeather.Wind;
        }

        #endregion

        #region Camera

        private async Task InitCamera()
        {
            //if (await CameraProvider.GetInstance().InitializeCameraAsync())
            //{
            //    CameraProvider.GetInstance().DetectedPeopleChanged += MainPage_EmotionDetected;
            //}
        }

        private void MainPage_EmotionDetected(object sender, PeopleDetectionEventArgs e)
        {
            UpdateBottomText(e.Emotions.First().ToString());
        }

        #endregion

        #region Speech

        private SpeechRecognizer _speechRecognizer;
        private SpeechRecognizer _continousSpeechRecognizer;
        private bool _speechInit = false;
        private ManualResetEvent manualResetEvent;

        private string DefaultBottomText = "go ahead, ask me anything";

        

        private async Task InitSpeech()
        {
            try
            {
                manualResetEvent = new ManualResetEvent(false);

                _continousSpeechRecognizer = new SpeechRecognizer();
                _continousSpeechRecognizer.Constraints.Add(new SpeechRecognitionListConstraint(new List<String>() { "Hey Em" }, "start"));
                var result = await _continousSpeechRecognizer.CompileConstraintsAsync();

                if (result.Status != SpeechRecognitionResultStatus.Success)
                {
                    Debug.WriteLine("Failed to init cont speech: " + result.Status.ToString());
                    return;
                }

                _speechRecognizer = new SpeechRecognizer();
                result = await _speechRecognizer.CompileConstraintsAsync();
                _speechRecognizer.HypothesisGenerated += _speechRecognizer_HypothesisGenerated;

                if (result.Status != SpeechRecognitionResultStatus.Success)
                {
                    Debug.WriteLine("Failed to init speech: " + result.Status.ToString());
                    return;
                }

                _continousSpeechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
                await _continousSpeechRecognizer.ContinuousRecognitionSession.StartAsync(SpeechContinuousRecognitionMode.Default);

                _speechInit = true;

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            if (args.Result.Confidence == SpeechRecognitionConfidence.High || args.Result.Confidence == SpeechRecognitionConfidence.Medium)
            {
                var t = BottomText.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ListenAndHandleRequest();
                });
            }

        }

        

        private void _speechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            UpdateBottomText(args.Hypothesis.Text);
        }

        private async Task ListenAndHandleRequest()
        {
            await _continousSpeechRecognizer.ContinuousRecognitionSession.CancelAsync();
            UpdateBottomText("listening...");

            MusicProvider.GetInstance().LowerVolume();

            Media.Source = new Uri("ms-appx:///Assets/Sounds/sound1.wav");

            var spokenText = await ListenForText();
            if (string.IsNullOrWhiteSpace(spokenText) ||
                spokenText.ToLower().Contains("cancel") ||
                spokenText.ToLower().Contains("never mind"))
            {
                UpdateBottomText("no problem");
            }
            else
            {
                UpdateBottomText("\"" + spokenText + "\"");

                var LUISTask = LUISProvider.GetInstance().GetAndHandleIntent(spokenText, this);
                var WolframTask = WolframProvider.GetInstance().GetSimpleResultForQuery(spokenText);

                List<Task> tasks = new List<Task>();
                tasks.Add(LUISTask);
                tasks.Add(WolframTask);

                await Task.WhenAll(tasks.ToArray());

                // if LUIS has not handled this request
                // try to get an answer from Wolfram Alpha
                if (!LUISTask.Result && WolframTask.Result != null)
                {
                    var result = WolframTask.Result;
                    
                    if (result.Image == null)
                        await RespondAsync(result.Text);
                    else
                        await RespondAsync(result.Text, result.Image);
                }
                else if (!LUISTask.Result)
                {
                    await RespondAsync("i don't know that yet!", true);
                }
            }

            MusicProvider.GetInstance().ResetVolume();

            await Task.Delay(3000);

            HideResponseGrid();
            UpdateBottomText(DefaultBottomText);
            await _continousSpeechRecognizer.ContinuousRecognitionSession.StartAsync();

        }

        private async Task<string> ListenForText()
        {
            string result = null;
            try
            {
                SpeechRecognitionResult speechRecognitionResult = await _speechRecognizer.RecognizeAsync();
                if (speechRecognitionResult.Status == SpeechRecognitionResultStatus.Success)
                {
                    result = speechRecognitionResult.Text;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return result;
        }

        private async Task SpeakAsync(string toSpeak)
        {
            SpeechSynthesizer speechSyntesizer = new SpeechSynthesizer();
            SpeechSynthesisStream syntStream = await speechSyntesizer.SynthesizeTextToStreamAsync(toSpeak);
            Media.SetSource(syntStream, syntStream.ContentType);

            Media.MediaEnded += Media_MediaEnded;

            Task t = Task.Run(() =>
            {
                manualResetEvent.Reset();
                manualResetEvent.WaitOne();
            });

            await t;
        }

        private void Media_MediaEnded(object sender, RoutedEventArgs e)
        {
            manualResetEvent.Set();
            Media.MediaEnded -= Media_MediaEnded;
        }

        #endregion

        #region Music

        private void InitMusic()
        {
            MusicProvider.GetInstance().MusicStarted += MainPage_MusicStarted;
            MusicProvider.GetInstance().MusicStoped += MainPage_MusicStoped;
        }

        private void MainPage_MusicStoped(object sender, EventArgs e)
        {
            var t = MusicInfo.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                MusicInfo.Visibility = Visibility.Collapsed;

            });
        }

        private void MainPage_MusicStarted(object sender, EventArgs e)
        {
            var t = MusicInfo.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                MusicInfo.Visibility = Visibility.Visible;
            });
        }


        #endregion

        #region IUserResponseConnection
        
        public async Task<bool> RespondAsync(string text, bool silent = false)
        {
            await ResponseGrid.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await HideResponseGrid();

                Response_Text.Visibility = Visibility.Visible;
                Response_Text.Text = text;

                Response_Subtext.Visibility = Visibility.Collapsed;

                Response_Image.Visibility = Visibility.Collapsed;

                await ShowResponseGrid();
            });

            if (!silent)
                await SpeakAsync(text);

            return true;
        }

        public async Task<bool> RespondAsync(string text, string subtext, bool silent = false)
        {
            await ResponseGrid.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await HideResponseGrid();

                Response_Text.Text = text;
                Response_Text.Visibility = Visibility.Visible;

                Response_Subtext.Text = subtext;
                Response_Subtext.Visibility = Visibility.Visible;

                Response_Image.Visibility = Visibility.Collapsed;

                await ShowResponseGrid();
            });

            if (!silent)
                await SpeakAsync(text);

            return true;
        }

        public async Task<bool> RespondAsync(string text, Uri imageUri, bool silent = false)
        {
            var image = await PreLoadImage(imageUri);
            await ResponseGrid.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await HideResponseGrid();

                Response_Text.Text = text;
                Response_Text.Visibility = Visibility.Visible;

                Response_Subtext.Visibility = Visibility.Collapsed;

                Response_Image.Source = image;
                Response_Image.Visibility = Visibility.Visible;

                await ShowResponseGrid();
            });

            if (!silent)
                await SpeakAsync(text);

            return true;
        }

        public async Task<string> AskForResponseAsync(string text)
        {
            await ResponseGrid.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

            });
            return "";
        }

        public async Task<string> AskForResponseAsync(string text, string subtext)
        {
            await ResponseGrid.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

            });
            return "";
        }

        public async Task<string> AskForResponseAsync(string text, Uri imageUri)
        {
            await ResponseGrid.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

            });
            return "";
        }

        private async Task HideResponseGrid()
        {
            ResponseGrid.Visibility = Visibility.Collapsed;
        }

        private async Task ShowResponseGrid()
        {
            ResponseGrid.Visibility = Visibility.Visible;
        }

        private void UpdateBottomText(string text)
        {
            var t = BottomText.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                BottomText.Text = text;

            });
        }

        private async Task<BitmapImage> PreLoadImage(Uri uri)
        {
            var bitmap = new BitmapImage();
            var file = await StorageFile.CreateStreamedFileFromUriAsync("image.jpg", uri, null);
            await bitmap.SetSourceAsync(await file.OpenReadAsync());
            return bitmap;
        }

        #endregion

    }
}
