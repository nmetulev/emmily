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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace emmily
{
    public sealed partial class MainPage : Page
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
            tasks.Add(InitLights());
            // init camera
            // init lights


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
            if (args.Result.Confidence == SpeechRecognitionConfidence.Medium ||
                args.Result.Confidence == SpeechRecognitionConfidence.High)
            {
                var t = BottomText.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    StartListening();
                });
            }

        }

        private async Task UpdateBottomText(string text, bool speak = false)
        {
            await BottomText.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                BottomText.Text = text;
                
            });

            if (speak)
                await SpeakAsync(text);
        }

        private void _speechRecognizer_HypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
        {
            UpdateBottomText(args.Hypothesis.Text);
        }

        private async Task StartListening()
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

                var LUISTask = LUISProvider.GetInstance().GetIntent(spokenText);
                var WolframTask = WolframProvider.GetInstance().GetSimpleResultForQuery(spokenText);


                List<Task> tasks = new List<Task>();
                tasks.Add(LUISTask);
                tasks.Add(WolframTask);

                await Task.WhenAll(tasks.ToArray());

                if (LUISTask.Result)
                {
                    UpdateBottomText("Done :)");
                }
                else
                {
                    var result = WolframTask.Result;
                    if (result != null)
                        await UpdateBottomText(result, true);
                    else
                        UpdateBottomText("I don't know what you are talking about wilis");
                }
            }

            MusicProvider.GetInstance().ResetVolume();

            await Task.Delay(3000);

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

        #region Lights

        private async Task InitLights()
        {
            var provider = LightProvider.GetInstance();
            await provider.FindAndConnectToLights();
        }

        #endregion


    }
}
