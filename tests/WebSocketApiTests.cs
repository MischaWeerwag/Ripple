using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public class WebSocketClientSetup : IDisposable
    {
        public readonly ClientWebSocket WebSocket;
        public readonly WebSocketApi SocketApi;

        public WebSocketClientSetup()
        {
            var address = new Uri("wss://s.altnet.rippletest.net:51233");
            WebSocket = new ClientWebSocket();
            WebSocket.ConnectAsync(address, CancellationToken.None).Wait();
            SocketApi = new WebSocketApi(WebSocket);
        }

        public void Dispose()
        {
            WebSocket.Dispose();
        }
    }


    public class WebSocketApiTests : IClassFixture<WebSocketClientSetup>
    {
        WebSocketClientSetup fixture;

        public WebSocketApiTests(WebSocketClientSetup fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async Task TestPing()
        {
            await fixture.SocketApi.Ping();
        }

        [Fact]
        public async Task TestRandom()
        {
            var random = await fixture.SocketApi.Random();
            Assert.NotEqual(default, random);
        }
    }
}
