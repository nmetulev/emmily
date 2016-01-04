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
                          APIKeys.LUISAppID + 
                          "&subscription-key=" + 
                          APIKeys.LUISSubscriptionKey + 
                          "&q=";
        }


        // returns handled or not
        public async Task<bool> GetAndHandleIntent(string query, IUserResponseConnection responseConnection)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var httpResponse = await client.GetStringAsync(new Uri(_partialUri + query));

                    var response = JsonConvert.DeserializeObject<LUISResponse>(httpResponse);

                    return await HandleIntent(response, responseConnection);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    return false;
                }
            }
        }

        private async Task<bool> HandleIntent(LUISResponse response, IUserResponseConnection responseConnection)
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
                    await HandleMusicIntent(intentArray[1], responseConnection);
                    return true;
                case "light":
                    await HandleLightIntent(intentArray[1], responseConnection);
                    return true;
                default:
                    return false;
            }
            
        }

        private async Task HandleLightIntent(string intent, IUserResponseConnection responseConnection)
        {
            var lightProvider = LightProvider.GetInstance();
            if (lightProvider.Status == LightProviderStatus.NotInitialized)
            {
                await lightProvider.FindAndConnectToLights();
            }
            if (lightProvider.Status == LightProviderStatus.NoBridgesFound)
            {
                await responseConnection.RespondAsync("I can't find any bridges", "Make sure any Hue bridges are turned on and connected");
                return;
            }
            else if (lightProvider.Status == LightProviderStatus.BridgeNotRegistered)
            {
                var registerResponse = responseConnection.RespondAsync("Sure, just press the bridge button this one time", "you have 30ish seconds, no pressure :)");
                var status = await lightProvider.RegisterApp();
                await registerResponse;

                if (status != LightProviderStatus.Connected)
                {
                    await responseConnection.RespondAsync("I don't think you pressed the button", "... but what do I know, I'm a mirror");
                    return;
                }
                else
                {
                    await responseConnection.RespondAsync("thanks", "now onto that action you wanted me to do");
                }
                // attempt to register
            }

            if (lightProvider.Status != LightProviderStatus.Connected)
            {
                await responseConnection.RespondAsync("Can't Connect for some reason");
                return;
            }

            switch (intent)
            {
                case "on":
                    await lightProvider.TurnOnLights();
                    await responseConnection.RespondAsync("light's are now on", true);
                    return;
                case "off":
                    await lightProvider.TurnOffLights();
                    await responseConnection.RespondAsync("light's are now off", true);
                    return;
                default:
                    return;
            }
        }

        private async Task HandleMusicIntent(string intent, IUserResponseConnection responseConnection)
        {
            switch (intent)
            {
                case "play":
                    MusicProvider.GetInstance().StartPlayback();
                    await responseConnection.RespondAsync("Party on ;)", true);
                    return;
                case "stop":
                    MusicProvider.GetInstance().StopPlayback();
                    await responseConnection.RespondAsync("Party off :(", true);
                    return;
                case "toggle":
                    if (MusicProvider.GetInstance().IsPlaying())
                        MusicProvider.GetInstance().StopPlayback();
                    else
                        MusicProvider.GetInstance().StartPlayback();
                    return;
                default:
                    return;
            } 
        }
    }
}
