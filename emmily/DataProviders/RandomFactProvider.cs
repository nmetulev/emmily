using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace emmily.DataProviders
{
    public class RandomFactProvider
    {
        private static RandomFactProvider _provider;

        public static RandomFactProvider GetInstance()
        {
            if (_provider == null)
                _provider = new RandomFactProvider();

            return _provider;
        }

        private RandomFactProvider()
        {

        }

        //http://codepen.io/LorenK/pen/EjmVwv
        //http://mentalfloss.com/api/1.0/views/amazing_facts.json?id=731&limit=1&display_id=xhr&cb=0.12346546545641321151

        //public string GetNewFact()
        //{
        //    using (var client = new HttpClient)
        //    {

        //    }
        //}
    }
}
