using emmily.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Connectivity;
using Windows.Web.Http;

namespace emmily.DataProviders
{
    public class LocationProvider
    {
        private static LocationProvider _provider;

        public static LocationProvider GetInstance()
        {
            if (_provider == null)
                _provider = new LocationProvider();

            return _provider;
        }

        private string _uriPartial = "https://freegeoip.net/json/";
        
        public async Task<GeoIP> GetLocationForCurrentIP()
        {
            var ip = await GetPublicIP();
            if (ip == null) return null;

            using (var client = new HttpClient())
            {
                var uri = new Uri(_uriPartial + ip);

                try
                {
                    var response = await client.GetStringAsync(uri);
                    return JsonConvert.DeserializeObject<GeoIP>(response);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return null;
                }
            }
            
        }

        private async Task<string> GetPublicIP()
        {
            using (var client = new HttpClient())
            {
                string response = null;

                try
                {
                   response = await client.GetStringAsync(new Uri("http://www.realip.info/api/p/realip.php"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return null;
                }
                var o = JsonConvert.DeserializeObject<IPData>(response);
                return o.IP;
            }
        }
    }


}
