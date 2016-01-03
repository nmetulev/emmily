using Q42.HueApi;
using Q42.HueApi.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace emmily.DataProviders
{
    public class LightProvider
    {
        private static LightProvider _provider;

        public static LightProvider GetInstance()
        {
            if (_provider == null)
                _provider = new LightProvider();

            return _provider;
        }

        private ILocalHueClient _client;
        private const string _storageKey = "hue.appKey";

        public LightProviderStatus Status { get; private set; }

        private LightProvider()
        {
            Status = LightProviderStatus.NotInitialized;
        }

        public async Task<LightProviderStatus> FindAndConnectToLights()
        {
            var locator = new HttpBridgeLocator();
            var bridgeIPs = await locator.LocateBridgesAsync(TimeSpan.FromSeconds(5));

            if (bridgeIPs.Count() > 0)
            {
                _client = new LocalHueClient(bridgeIPs.First());

                var localSettings = ApplicationData.Current.LocalSettings;

                object appKey = localSettings.Values[_storageKey];

                if (appKey != null)
                {
                    _client.Initialize(appKey as string);
                    var command = new LightCommand();
                    command.Alert = Alert.Once;
                    Status = await SendCommandHelper(command);
                }
                else
                {
                    Status = LightProviderStatus.BridgeNotRegistered;
                }
            }
            else
                Status = LightProviderStatus.NoBridgesFound;

            return Status;
        }

        public async Task<LightProviderStatus> RegisterApp(int timeout = 30)
        {
            if (Status != LightProviderStatus.BridgeNotRegistered) return Status;

            string appKey = null;

            bool success = false;

            var now = DateTime.Now;

            while (!success && (DateTime.Now - now).Seconds < timeout)
            {
                try
                {
                    appKey = await _client.RegisterAsync("emmily", "mm");
                    success = true;
                }
                catch (Exception ex)
                {
                    
                }
                await Task.Delay(1000);
            }


            if (appKey != null)
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[_storageKey] = appKey;
                Status = LightProviderStatus.Connected;
            }

            return Status;
        }

        public async Task<LightProviderStatus> TurnOnLights()
        {
            if (Status != LightProviderStatus.Connected)
            {
                return Status;
            }
            var command = new LightCommand();
            command.TurnOn();

            return await SendCommandHelper(command);
        }

        public async Task<LightProviderStatus> TurnOffLights()
        {
            if (Status != LightProviderStatus.Connected)
            {
                return Status;
            }
            var command = new LightCommand();
            command.TurnOff();

            return await SendCommandHelper(command);
        }

        private async Task<LightProviderStatus> SendCommandHelper(LightCommand command)
        {
            var result = await _client.SendCommandAsync(command);

            //if (result.Count > 0 && result.First().Success != null)
            //TODO: need to figure out the statuses
            Status = LightProviderStatus.Connected;
            return Status;
        }
    }

    public enum LightProviderStatus
    {
        NotInitialized,
        NoBridgesFound,
        BridgeNotRegistered,
        Connected
    }
}
