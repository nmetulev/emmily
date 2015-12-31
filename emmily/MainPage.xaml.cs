using emmily.Common;
using emmily.Data;
using emmily.DataProviders;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

            List<Task> tasks = new List<Task>();
            tasks.Add(InitWeather());
            // init camera
            // init speech
            // init network
            // init ui

            await Task.WhenAll(tasks.ToArray());

            LoadingScreen.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
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
    }
}
