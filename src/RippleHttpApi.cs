using System;
using System.Net.Http;

namespace Ibasa.Ripple
{
    public sealed class RippleHttpApi
    {
        private readonly HttpClient client;
        public RippleHttpApi(HttpClient httpClient)
        {
            client = httpClient;
        }
    }
}
