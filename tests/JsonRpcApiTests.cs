using System;
using System.Net.Http;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class JsonRpcApiTestsSetup : ApiTestsSetup<JsonRpcApi>
    {
        protected override JsonRpcApi CreateApi()
        {
            var address = new Uri("https://s.altnet.rippletest.net:51234");
            var httpClient = new HttpClient();
            httpClient.BaseAddress = address;
            return new JsonRpcApi(httpClient);
        }
    }

    [Collection("JsonRpc")]
    public class JsonRpcApiTests : ApiTests<JsonRpcApi>, IClassFixture<JsonRpcApiTestsSetup>
    {
        public JsonRpcApiTests(JsonRpcApiTestsSetup setup) : base(setup)
        {
        }
    }
}
