using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WolframAlpha.Api.v2;
using WolframAlpha.Api.v2.Components;
using WolframAlpha.Api.v2.Requests;

namespace emmily.DataProviders
{
    public class WolframProvider
    {
        private static WolframProvider _provider;
        private QueryRequest _request;

        public static WolframProvider GetInstance()
        {
            if (_provider == null)
                _provider = new WolframProvider();

            return _provider;
        }

        private WolframProvider()
        {
            _request = new QueryRequest();
        }

        public async Task<WolframResponse> GetSimpleResultForQuery(string query)
        {
            try
            {
                var b = new QueryBuilder();
                b.AppId = APIKeys.WolframAPIKey;
                b.Input = query;

                var result = await _request.ExecuteAsync(b.QueryUri);

                if (result == null ||
                    result.Success == "false" ||
                    result.Pods.Count() == 0) return null;

                var response = new WolframResponse();

                var resultPod = result.Pods.FirstOrDefault(p => p.Title == "Result");
                var factsPod = result.Pods.FirstOrDefault(p => p.Title == "Notable facts");
                var basicPod = result.Pods.FirstOrDefault(p => p.Title == "Basic information");

                var resultText = GetPlainTextFromPod(resultPod);
                var factsText = GetPlainTextFromPod(factsPod);
                var basicText = GetPlainTextFromPod(basicPod);

                if (resultText != null)
                {
                    response.Text = resultText;
                }
                else if (factsText != null)
                {
                    response.Text = factsText.Split(new[] { '\r', '\n', '.' }).FirstOrDefault();

                    response.SubText = basicText;
                }
                else if (basicText != null)
                {
                    response.Text = basicText;
                }
                else
                    return null;

                var imagePod = result.Pods.FirstOrDefault(p => p.Title == "Image");
                if (imagePod != null && imagePod.SubPods.Count() > 0)
                {
                    var imageSubPod = imagePod.SubPods.First();

                    if (imageSubPod.Img != null)
                    {
                        response.Image = new Uri(imageSubPod.Img.Src);
                    }
                }

                return response;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
            

        }

        private string GetPlainTextFromPod(Pod pod)
        {
            if (pod != null &&
                pod.SubPods.Count() != 0 &&
                !string.IsNullOrWhiteSpace(pod.SubPods[0].PlainText))
            {
                return pod.SubPods[0].PlainText;
            }
            return null;
        }
        
    }

    public class WolframResponse
    {
        public string Text { get; set; }
        public string SubText { get; set; }
        public Uri Image { get; set; }
    }
}
