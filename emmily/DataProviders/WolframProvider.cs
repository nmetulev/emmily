using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WolframAlpha.Api.v2;
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

        public async Task<string> GetSimpleResultForQuery(string query)
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

                var resultPod = result.Pods.FirstOrDefault(p => p.Title == "Result");

                if (resultPod == null || resultPod.SubPods.Count() == 0) return null;

                var resultSubPod = resultPod.SubPods.First();

                if (string.IsNullOrWhiteSpace(resultSubPod.PlainText)) return null;

                return resultSubPod.PlainText;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
            

        }
    }
}
