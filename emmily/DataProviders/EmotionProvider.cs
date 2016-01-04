using Microsoft.ProjectOxford.Emotion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Streams;

namespace emmily.DataProviders
{
    public class EmotionProvider
    {
        private static EmotionProvider _provider;

        public static EmotionProvider GetInstance()
        {
            if (_provider == null)
                _provider = new EmotionProvider();

            return _provider;
        }

        private EmotionServiceClient _client;

        private EmotionProvider()
        {
            _client = new EmotionServiceClient(APIKeys.ProjectOxfordEmotionAPIKey);
        }

        public async Task<List<Emotion>> IdentifyEmotionAsync(Stream stream)
        {
            var result = await _client.RecognizeAsync(stream);

            var emotions = new List<Emotion>();

            foreach (var score in result)
            {
                emotions.Add(GetTopEmotion(score.Scores));
            }

            return emotions;
        }

        private Emotion GetTopEmotion(Microsoft.ProjectOxford.Emotion.Contract.Scores scores)
        {
            var max = scores.Anger;
            Emotion emotion = Emotion.Anger;
            if (scores.Contempt > max)
            {
                max = scores.Contempt;
                emotion = Emotion.Contempt;
            }
            if (scores.Disgust > max)
            {
                max = scores.Disgust;
                emotion = Emotion.Disgust;
            }
            if (scores.Fear > max)
            {
                max = scores.Fear;
                emotion = Emotion.Fear;
            }
            if (scores.Happiness > max)
            {
                max = scores.Happiness;
                emotion = Emotion.Happiness;
            }
            if (scores.Neutral > max)
            {
                max = scores.Neutral;
                emotion = Emotion.Neutral;
            }
            if (scores.Sadness > max)
            {
                max = scores.Sadness;
                emotion = Emotion.Sadness;
            }
            if (scores.Surprise > max)
            {
                max = scores.Surprise;
                emotion = Emotion.Surprise;
            }

            return emotion;
        }
    }

    public enum Emotion
    {
        Anger,
        Contempt,
        Disgust,
        Fear,
        Happiness,
        Neutral,
        Sadness,
        Surprise
    }
}
