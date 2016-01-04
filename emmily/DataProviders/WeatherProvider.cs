using emmily.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WeatherNet.Clients;
using Windows.UI.Xaml;

namespace emmily.DataProviders
{

    public class WeatherProvider
    {
        private static WeatherProvider _provider;

        public static WeatherProvider GetInstance()
        {
            if (_provider == null)
                _provider = new WeatherProvider();

            return _provider;
        }

        public WeatherData CurrentData { get; private set; }

        public event EventHandler WeatherRefreshed;

        private DispatcherTimer _refreshTimer;
        private bool _updatingCurrentWeather = false;

        private GeoIP _geoLocation;

        private WeatherProvider()
        {
            WeatherNet.ClientSettings.ApiKey = APIKeys.WeatherAPIKey;

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMinutes(Settings.WeatherRefreshInterval);
            _refreshTimer.Tick += _refreshTimer_Tick;
            _refreshTimer.Start();
        }

        private async void _refreshTimer_Tick(object sender, object e)
        {
            if (_updatingCurrentWeather) return;
            _updatingCurrentWeather = true;

            var data = await UpdateCurrentWeatherNow();
            if (data != null && WeatherRefreshed != null)
            {
                WeatherRefreshed(this, new EventArgs());
            }

            _updatingCurrentWeather = false;
        }

        public async Task<WeatherData> UpdateCurrentWeatherNow()
        {
            if (_geoLocation == null)
            {
                _geoLocation = await LocationProvider.GetInstance().GetLocationForCurrentIP();
                if (_geoLocation == null) return null;
            }
            

            var data = new WeatherData();
            data.Location = _geoLocation.city;

            var currentWeatherTask = WeatherNet.Clients.CurrentWeather.GetByCityName(_geoLocation.city, _geoLocation.country_name, "en", Settings.Unit.ToString().ToLower());
            //var fiveDayWeatherTask = WeatherNet.Clients.FiveDaysForecast.GetByCityNameAsync(_geoLocation.city, _geoLocation.country_name, "en", Settings.Unit.ToString().ToLower());

            List<Task> tasks = new List<Task>();
            tasks.Add(currentWeatherTask);
            //tasks.Add(fiveDayWeatherTask);

            await Task.WhenAll(tasks.ToArray());

            var currentWeatherData = currentWeatherTask.Result;

            if (!currentWeatherData.Success) return null;

            data.Temperature = (int) Math.Round(currentWeatherData.Item.Temp);
            data.LowTemperature = (int) Math.Round(currentWeatherData.Item.TempMin);
            data.HighTemperature = (int)Math.Round(currentWeatherData.Item.TempMax);
            data.Unit = Settings.Unit == Units.Metric ? "°C" : "°F";
            data.Location = currentWeatherData.Item.City + ", " + currentWeatherData.Item.Country;
            data.Description = currentWeatherData.Item.Description;
            data.Wind = currentWeatherData.Item.WindSpeed.ToString() + (Settings.Unit == Units.Metric ? " m/s" : " mph");

            //var fiveDayWeaterForecast = fiveDayWeatherTask.Result;

            //if (fiveDayWeaterForecast.Success)
            //{
            //    data.FiveDayForecast = new List<WeatherData>();
            //    foreach (var forecast in fiveDayWeaterForecast.Items)
            //    {
            //        var dayData = new WeatherData();
            //        dayData.Temperature = (int) Math.Round(forecast.Temp);
            //        dayData.HighTemperature = (int) Math.Round(forecast.TempMax);
            //        dayData.LowTemperature = (int)Math.Round(forecast.TempMin);
            //        dayData.Time = forecast.Date.ToLocalTime();

            //        data.FiveDayForecast.Add(dayData);
            //    }
            //}

            CurrentData = data;

            return data;
        }
    }
}
