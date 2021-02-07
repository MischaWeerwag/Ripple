using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class WebSocketApiTestsSetup : ApiTestsSetup<WebSocketApi>
    {
        protected override WebSocketApi CreateApi()
        {
            var address = new Uri("wss://s.altnet.rippletest.net:51233");
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.ConnectAsync(address, CancellationToken.None).Wait();
            return new WebSocketApi(clientWebSocket);
        }
    }

    [Collection("WebSocket")]
    public class WebSocketApiTests : ApiTests<WebSocketApi>, IClassFixture<WebSocketApiTestsSetup>
    {
        public WebSocketApiTests(WebSocketApiTestsSetup setup) : base(setup)
        {
        }
    }
}
