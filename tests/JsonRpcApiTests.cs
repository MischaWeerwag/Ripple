using System;
using System.Net.Http;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class JsonRpcApiTestsSetup : ApiTestsSetup, IDisposable
    {
        public readonly JsonRpcApi RpcApi;

        public override Api Api {  get { return RpcApi; } }

        public JsonRpcApiTestsSetup()
        {
            var address = new Uri("https://s.altnet.rippletest.net:51234");
            var httpClient = new HttpClient();
            httpClient.BaseAddress = address;
            RpcApi = new JsonRpcApi(httpClient);
        }

        public void Dispose()
        {
            RpcApi.DisposeAsync().AsTask().Wait();
        }
    }

    [Collection("JsonRpc")]
    public class JsonRpcApiTests : ApiTests, IClassFixture<JsonRpcApiTestsSetup>
    {
        readonly new JsonRpcApi Api;

        public JsonRpcApiTests(JsonRpcApiTestsSetup setup) : base(setup)
        {
            this.Api = setup.RpcApi;
        }
    }
}
