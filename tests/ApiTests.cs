using System;
using System.Net.Http;
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

    public class TestAccountSetup
    {
        static readonly HttpClient HttpClient = new HttpClient();

        public readonly TestAccount TestAccountOne;
        public readonly TestAccount TestAccountTwo;

        public TestAccountSetup()
        {
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
    }

    public abstract class ApiTests : IClassFixture<TestAccountSetup>
    {
        protected readonly TestAccountSetup TestAccounts;
        protected readonly Api Api;

        public ApiTests(Api api, TestAccountSetup testAccounts)
        {
            Api = api;
            TestAccounts = testAccounts;
        }

        [Fact]
        public async Task TestPing()
        {
            await Api.Ping();
        }

        [Fact]
        public async Task TestRandom()
        {
            var random = await Api.Random();
            Assert.NotEqual(default, random);
        }

        [Fact]
        public async Task TestAccount()
        {
            var account = new AccountID(TestAccounts.TestAccountOne.Address);

            var request = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await Api.AccountInfo(request);
            Assert.Equal(account, response.AccountData.Account);
            Assert.Equal(TestAccounts.TestAccountOne.Amount, response.AccountData.Balance);
        }

        [Fact]
        public async Task TestAccountCurrencies()
        {
            var account = new AccountID(TestAccounts.TestAccountOne.Address);
            var request = new AccountCurrenciesRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await Api.AccountCurrencies(request);
            Assert.False(response.Validated);
            Assert.Empty(response.SendCurrencies);
            Assert.Empty(response.ReceiveCurrencies);
            Assert.Null(response.LedgerHash);
            Assert.NotEqual(default, response.LedgerIndex);
        }

        [Fact]
        public async Task TestServerState()
        {
            var response = await Api.ServerState();
            Assert.NotEmpty(response.BuildVersion);
            Assert.NotEmpty(response.PubkeyNode);
            Assert.Null(response.PubkeyValidator);
            Assert.NotEqual(TimeSpan.Zero, response.ServerStateDuration);
            Assert.NotEqual(TimeSpan.Zero, response.Uptime);
        }

        [Fact]
        public async Task TestFee()
        {
            var response = await Api.Fee();
            Assert.NotEqual(0u, response.LedgerCurrentIndex);
        }
    }
}
