using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class WebSocketApiTestsSetup : ApiTestsSetup, IDisposable
    {
        public readonly WebSocketApi SocketApi;
        public override Api Api { get { return SocketApi; } }

        public WebSocketApiTestsSetup()
        {
            var address = new Uri("wss://s.altnet.rippletest.net:51233");
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.ConnectAsync(address, CancellationToken.None).Wait();
            SocketApi = new WebSocketApi(clientWebSocket);
        }

        public void Dispose()
        {
            SocketApi.DisposeAsync().AsTask().Wait();
        }
    }

    [Collection("WebSocket")]
    public class WebSocketApiTests : ApiTests, IClassFixture<WebSocketApiTestsSetup>
    {
        readonly new WebSocketApi Api;

        public WebSocketApiTests(WebSocketApiTestsSetup setup) : base(setup)
        {
            this.Api = setup.SocketApi;
        }
    }
}
