using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public struct TestAccount
    {
        public readonly string Address;
        public readonly string Secret;
        public readonly ulong Amount;

        public TestAccount(string address, string secret, ulong amount)
        {
            Address = address;
            Secret = secret;
            Amount = amount;
        }
    }

    public class WebSocketClientSetup : IDisposable
    {
        static readonly HttpClient HttpClient = new HttpClient();

        public readonly ClientWebSocket WebSocket;
        public readonly WebSocketApi SocketApi;

        public readonly TestAccount TestAccountOne;
        public readonly TestAccount TestAccountTwo;

        public WebSocketClientSetup()
        {
            var address = new Uri("wss://s.altnet.rippletest.net:51233");
            WebSocket = new ClientWebSocket();
            WebSocket.ConnectAsync(address, CancellationToken.None).Wait();
            SocketApi = new WebSocketApi(WebSocket);

            TestAccountOne = CreateAccount();
            TestAccountTwo = CreateAccount();
        }

        TestAccount CreateAccount()
        {
            var response = HttpClient.PostAsync("https://faucet.altnet.rippletest.net/accounts", null).Result;
            var json = response.Content.ReadAsStringAsync().Result;
            var document = System.Text.Json.JsonDocument.Parse(json);
            return new TestAccount(
                document.RootElement.GetProperty("account").GetProperty("address").GetString(),
                document.RootElement.GetProperty("account").GetProperty("secret").GetString(),
                document.RootElement.GetProperty("balance").GetUInt64() * 1000000UL);
        }

        public void Dispose()
        {
            SocketApi.Dispose();
        }
    }

    [Collection("WebSocket")]
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

        [Fact]
        public async Task TestAccount()
        {
            var account = new AccountID(fixture.TestAccountOne.Address);

            var request = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await fixture.SocketApi.AccountInfo(request);
            Assert.Equal(account, response.AccountData.Account);
            Assert.Equal(fixture.TestAccountOne.Amount, response.AccountData.Balance);
        }

        [Fact]
        public async Task TestAccountCurrencies()
        {
            var account = new AccountID(fixture.TestAccountOne.Address);
            var request = new AccountCurrenciesRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await fixture.SocketApi.AccountCurrencies(request);
            Assert.False(response.Validated);
            Assert.Empty(response.SendCurrencies);
            Assert.Empty(response.ReceiveCurrencies);
            Assert.Null(response.LedgerHash);
            Assert.NotEqual(default, response.LedgerIndex);
        }

        [Fact]
        public async Task TestServerState()
        {
            var response = await fixture.SocketApi.ServerState();
            Assert.NotEmpty(response.BuildVersion);
            Assert.NotEmpty(response.PubkeyNode);
            Assert.Null(response.PubkeyValidator);
            Assert.NotEqual(TimeSpan.Zero, response.ServerStateDuration);
            Assert.NotEqual(TimeSpan.Zero, response.Uptime);
        }

        [Fact]
        public async Task TestFee()
        {
            var response = await fixture.SocketApi.Fee();
            Assert.NotEqual(0u, response.LedgerCurrentIndex);
        }
    }
}
