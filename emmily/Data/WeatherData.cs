using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace emmily.Data
{
    public class WeatherData
    {
        public int Temperature { get; set; }
        public int LowTemperature { get; set; }
        public int HighTemperature { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public string Location { get; set; }
        public string Wind { get; set; }
        public DateTime Time { get; set; }
        public List<WeatherData> FiveDayForecast { get; set; }
    }
}
