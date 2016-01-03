using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace emmily.Data
{
    public class Settings
    {
        public static Units Unit = Units.Metric;

        public static TimeFormat TimeFormat = TimeFormat._12;

        public static int WeatherRefreshInterval = 15; // in minutes
        
    }

    public enum Units
    {
        Metric,
        Imperial
    }

    public enum TimeFormat
    {
        _12,
        _24
    }
}
