using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Playback;

namespace emmily.DataProviders
{
    public class MusicProvider
    {
        private static MusicProvider _provider;

        public static MusicProvider GetInstance()
        {
            if (_provider == null)
                _provider = new MusicProvider();

            return _provider;
        }

        public event EventHandler MusicStarted;
        public event EventHandler MusicStoped;


        private MusicProvider()
        {
            BackgroundMediaPlayer.Current.CurrentStateChanged += Current_CurrentStateChanged;
            BackgroundMediaPlayer.Current.Volume = 1;
        }

        private void Current_CurrentStateChanged(MediaPlayer sender, object args)
        {
            if (sender.CurrentState == MediaPlayerState.Playing)
            {
                if (MusicStarted != null) MusicStarted(this, null);
            }
            else
            {
                if (MusicStarted != null) MusicStoped(this, null);

            }
        }

        public void StartPlayback()
        {
            BackgroundMediaPlayer.Current.SetUriSource(new Uri("http://knhc.streamguys1.com/live"));
            BackgroundMediaPlayer.Current.Play();
        }

        public void StopPlayback()
        {
            BackgroundMediaPlayer.Current.Pause();
        }

        public void LowerVolume()
        {
            BackgroundMediaPlayer.Current.Volume = 0.1;
        }

        public void ResetVolume()
        {
            
            BackgroundMediaPlayer.Current.Volume = 1;
        }

        public bool IsPlaying()
        {
            return BackgroundMediaPlayer.Current.CurrentState == MediaPlayerState.Playing;
        }
    }
}
