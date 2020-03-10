using System;
using System.Net.Http;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class JsonRpcApiSetup : IDisposable
    {
        public readonly JsonRpcApi RpcApi;

        public JsonRpcApiSetup()
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
    public class JsonRpcApiTests : ApiTests, IClassFixture<JsonRpcApiSetup>
    {
        readonly new JsonRpcApi Api;

        public JsonRpcApiTests(JsonRpcApiSetup uut, TestAccountSetup testAccountSetup) : base(uut.RpcApi, testAccountSetup)
        {
            this.Api = uut.RpcApi;
        }
    }
}
