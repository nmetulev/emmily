using emmily.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Web.Http;

namespace emmily.DataProviders
{
    public class LUISProvider
    {
        private static LUISProvider _provider;

        public static LUISProvider GetInstance()
        {
            if (_provider == null)
                _provider = new LUISProvider();

            return _provider;
        }

        private string _partialUri;

        private const double _scoreThreshold = 0.8;

        private LUISProvider()
        {
            _partialUri = "https://api.projectoxford.ai/luis/v1/application?id=" + 
                          APIKeys.CortanaLUISAppID + 
                          "&subscription-key=" + 
                          APIKeys.CortanaLUISSubscriptionKey + 
                          "&q=";
        }


        // returns handled or not
        public async Task<bool> GetIntent(string query)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var httpResponse = await client.GetStringAsync(new Uri(_partialUri + query));

                    var response = JsonConvert.DeserializeObject<LUISResponse>(httpResponse);

                    return await HandleIntent(response);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return false;
                }
            }
        }

        private async Task<bool> HandleIntent(LUISResponse response)
        {
            if (response == null || response.intents.Count == 0) return false;

            string intent;

            if (response.intents.Count > 0)
            {
                var scoredIntent = response.intents.OrderByDescending(item => item.score).First();
                if (scoredIntent.score < _scoreThreshold) return false;

                intent = scoredIntent.intent;
            }
            else
            {
                intent = response.intents.First().intent;
            }

            var intentArray = intent.Split('.');

            if (intentArray.Count() < 2) return false;

            switch (intentArray[0])
            {
                case "music":
                    return HandleMusicIntent(intentArray[1]);
                case "light":
                    return await HandleLightIntent(intentArray[1]);
                default:
                    return false;
            }
            
        }

        private async Task<bool> HandleLightIntent(string intent)
        {
            var lightProvider = LightProvider.GetInstance();
            if (lightProvider.Status == LightProviderStatus.NotInitialized)
            {
                await lightProvider.FindAndConnectToLights();
            }
            if (lightProvider.Status == LightProviderStatus.NoBridgesFound)
            {
                // respond that the bridge is not found
                return false;
            }
            else if (lightProvider.Status == LightProviderStatus.BridgeNotRegistered)
            {
                var status = await lightProvider.RegisterApp();
                // attempt to register
            }

            if (lightProvider.Status != LightProviderStatus.Connected)
            {
                // respond with can't connect, no idea why
                return false;
            }

            switch (intent)
            {
                case "on":
                    await lightProvider.TurnOnLights();
                    return true;
                case "off":
                    await lightProvider.TurnOffLights();
                    return true;
                default:
                    return false;
            }
        }

        private bool HandleMusicIntent(string intent)
        {
            switch (intent)
            {
                case "play":
                    MusicProvider.GetInstance().StartPlayback();
                    return true;
                case "stop":
                    MusicProvider.GetInstance().StopPlayback();
                    return true;
                case "toggle":
                    if (MusicProvider.GetInstance().IsPlaying())
                        MusicProvider.GetInstance().StopPlayback();
                    else
                        MusicProvider.GetInstance().StartPlayback();
                    return true;
                default:
                    return false;
            } 
        }
    }
}
