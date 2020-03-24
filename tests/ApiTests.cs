using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public struct TestAccount
    {
        static readonly HttpClient HttpClient = new HttpClient();

        public readonly string Address;
        public readonly string Secret;
        public readonly ulong Amount;

        private TestAccount(string address, string secret, ulong amount)
        {
            Address = address;
            Secret = secret;
            Amount = amount;
        }

        public static TestAccount Create()
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

    public abstract class ApiTestsSetup
    {
        public readonly TestAccount TestAccountOne;
        public readonly TestAccount TestAccountTwo;

        public abstract Api Api { get; }

        public ApiTestsSetup()
        {
            TestAccountOne = TestAccount.Create();
            TestAccountTwo = TestAccount.Create();
        }
    }

    public abstract class ApiTests
    {
        protected readonly ApiTestsSetup Setup;
        protected Api Api { get { return Setup.Api; } }

        public ApiTests(ApiTestsSetup setup)
        {
            Setup = setup;
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
        public async Task TestFee()
        {
            var response = await Api.Fee();
            Assert.NotEqual(0u, response.LedgerCurrentIndex);
        }

        [Fact]
        public async Task TestLedgerCurrentAndClosed()
        {
            var current = await Api.LedgerCurrent();
            var closed = await Api.LedgerClosed();

            Assert.True(current > closed.LedgerIndex, "current > closed");
            Assert.NotEqual(default, closed.LedgerHash);
        }

        [Fact]
        public async Task TestAccount()
        {
            var account = new AccountId(Setup.TestAccountOne.Address);

            var request = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await Api.AccountInfo(request);
            Assert.Equal(account, response.AccountData.Account);
            Assert.Equal(Setup.TestAccountOne.Amount, response.AccountData.Balance);
        }

        [Fact]
        public async Task TestAccountCurrencies()
        {
            var account = new AccountId(Setup.TestAccountOne.Address);
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
        public async Task TestAccountLines()
        {
            // TODO: This isn't a very interesting test. We should get Submit TrustSet working and then use this to see the result.

            var request = new AccountLinesRequest();
            request.Account = new AccountId(Setup.TestAccountOne.Address);
            var response = await Api.AccountLines(request);

            Assert.Equal(request.Account, response.Account);
            var lines = new List<TrustLine>();
            await foreach(var line in response)
            {
                lines.Add(line);
            }
            Assert.Empty(lines);
        }

        [Fact]
        public async Task TestAccountSet()
        {
            var account = new AccountId(Setup.TestAccountOne.Address);
            var secret = new Seed(Setup.TestAccountOne.Secret);

            var infoRequest = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var infoResponse = await Api.AccountInfo(infoRequest);
            var feeResponse = await Api.Fee();

            var transaction = new AccountSet();
            transaction.Account = account;
            transaction.Sequence = infoResponse.AccountData.Sequence;
            transaction.Domain = System.Text.Encoding.ASCII.GetBytes("example.com");
            transaction.Fee = feeResponse.Drops.MedianFee;
            var submitRequest = new SubmitRequest();
            submitRequest.TxBlob = transaction.Sign(secret, out var transactionHash);
            var submitResponse = await Api.Submit(submitRequest);

            Assert.Equal(submitRequest.TxBlob, submitResponse.TxBlob);
            Assert.Equal("tesSUCCESS", submitResponse.EngineResult);

            while(true)
            {
                try
                {
                    var tx = await Api.Tx(transactionHash);
                    Assert.Equal(tx.Hash, transactionHash);
                    break;
                }
                catch
                {
                    continue;
                }
            }

            infoRequest = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            infoResponse = await Api.AccountInfo(infoRequest);
            Assert.Equal(account, infoResponse.AccountData.Account);
            Assert.Equal(transaction.Domain, infoResponse.AccountData.Domain);
        }
    }
}
