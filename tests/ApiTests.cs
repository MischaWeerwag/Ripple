using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using Xunit;

namespace Ibasa.Ripple.Tests
{
    public struct TestAccount
    {
        static readonly HttpClient HttpClient = new HttpClient();

        public readonly AccountId Address;
        public readonly Seed Secret;
        public readonly ulong Amount;

        private TestAccount(AccountId address, Seed secret, ulong amount)
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
                new AccountId(document.RootElement.GetProperty("account").GetProperty("address").GetString()),
                new Seed(document.RootElement.GetProperty("account").GetProperty("secret").GetString()),
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

        private async Task AutofillTransaction(Transaction transaction)
        {
            var request = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = transaction.Account,
            };
            var infoResponse = await Api.AccountInfo(request);
            var feeResponse = await Api.Fee();

            transaction.Sequence = infoResponse.AccountData.Sequence;
            transaction.Fee = feeResponse.Drops.MedianFee;
        }

        private async Task<TransactionResponse> WaitForTransaction(Hash256 transaction)
        {
            while (true)
            {
                var request = new TxRequest()
                {
                    Transaction = transaction
                };
                var transactionResponse = await Api.Tx(request);
                Assert.Equal(transaction, transactionResponse.Hash);
                if (transactionResponse.Validated) 
                {
                    return transactionResponse;
                }
            }
        }

        private Task<SubmitResponse> SubmitTransaction(Seed secret, Transaction transaction, out Hash256 transactionHash)
        {
            var request = new SubmitRequest();
            secret.Secp256k1KeyPair(out var _, out var keyPair);
            request.TxBlob = transaction.Sign(keyPair, out transactionHash);
            return Api.Submit(request);
        }

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
        public async Task TestLedger_Order()
        {
            var request = new LedgerRequest();

            request.Ledger = LedgerSpecification.Validated;
            var validated = await Api.Ledger(request);
            request.Ledger = LedgerSpecification.Closed;
            var closed = await Api.Ledger(request);
            request.Ledger = LedgerSpecification.Current;
            var current = await Api.Ledger(request);

            Assert.True(validated.Validated);
            Assert.True(validated.Closed);

            // Closed might be validated as well
            Assert.True(closed.Closed);

            Assert.False(current.Validated);
            Assert.False(current.Closed);

            // Index increases
            Assert.True(validated.LedgerIndex <= closed.LedgerIndex);
            Assert.True(closed.LedgerIndex < current.LedgerIndex);

            // Time goes foward
            Assert.True(validated.Ledger.CloseTime <= closed.Ledger.CloseTime);

            // Coins go down
            Assert.True(validated.Ledger.TotalCoins >= closed.Ledger.TotalCoins);
        }

        [Fact]
        public async Task TestLedger_Transactions()
        {
            var request = new LedgerRequest();
            request.Ledger = LedgerSpecification.Validated;
            request.Transactions = true;
            var response = await Api.Ledger(request);

            Assert.NotNull(response.Transactions);
            foreach(var tx in response.Transactions)
            {
                Assert.NotEqual(default, tx);
            }
        }

        [Fact]
        public async Task TestAccountInfo()
        {
            var account = Setup.TestAccountOne.Address;

            var request = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await Api.AccountInfo(request);
            Assert.Equal(account, response.AccountData.Account);
        }

        [Fact]
        public async Task TestAccountCurrencies()
        {
            var account = Setup.TestAccountOne.Address;
            var request = new AccountCurrenciesRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await Api.AccountCurrencies(request);
            Assert.False(response.Validated);
            // Not empty, we might of done the GBP test first
            // Assert.Empty(response.SendCurrencies);
            // Assert.Empty(response.ReceiveCurrencies);
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
            request.Account = Setup.TestAccountOne.Address;
            var response = await Api.AccountLines(request);

            Assert.Equal(request.Account, response.Account);
            var lines = new List<TrustLine>();
            await foreach(var line in response)
            {
                lines.Add(line);
            }

            // Not empty, we might of done the GBP test first
            // Assert.Empty(lines);
        }

        [Fact]
        public async Task TestSubmit()
        {
            // Example tx blob from ripple documentation (https://xrpl.org/submit.html)
            var hex = "1200002280000000240000001E61D4838D7EA4C6800000000000000000000000000055534400000000004B4E9C06F24296074F7BC48F92A97916C6DC5EA968400000000000000B732103AB40A0490F9B7ED8DF29D246BF2D6269820A0EE7742ACDD457BEA7C7D0931EDB7447304502210095D23D8AF107DF50651F266259CC7139D0CD0C64ABBA3A958156352A0D95A21E02207FCF9B77D7510380E49FF250C21B57169E14E9B4ACFD314CEDC79DDD0A38B8A681144B4E9C06F24296074F7BC48F92A97916C6DC5EA983143E9D4A2B8AA0780F682D136F7A56D6724EF53754";
            var utf8 = System.Text.Encoding.UTF8.GetBytes(hex);
            var txBlob = new byte[Base16.GetMaxDecodedFromUtf8Length(utf8.Length)];
            var status = Base16.DecodeFromUtf8(utf8, txBlob, out var _, out var _);
            Assert.Equal(System.Buffers.OperationStatus.Done, status);

            var submitRequest = new SubmitRequest();
            submitRequest.TxBlob = txBlob;
            var submitResponse = await Api.Submit(submitRequest);

            Assert.Equal(submitRequest.TxBlob, submitResponse.TxBlob);
            Assert.Equal(EngineResult.terPRE_SEQ, submitResponse.EngineResult);
        }

        [Fact]
        public async Task TestAccountSet()
        {
            var account = Setup.TestAccountOne.Address;
            var secret = Setup.TestAccountOne.Secret;

            var transaction = new AccountSet();
            transaction.Account = account;
            transaction.Domain = System.Text.Encoding.ASCII.GetBytes("example.com");
            await AutofillTransaction(transaction);

            var submitResponse = await SubmitTransaction(secret, transaction, out var transactionHash);

            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var transactionResponse = await WaitForTransaction(transactionHash);
            var acr = Assert.IsType<AccountSet>(transactionResponse.Transaction);
            Assert.Equal(transaction.Account, acr.Account);
            Assert.Equal(transaction.Sequence, acr.Sequence);
            Assert.Equal(transaction.Fee, acr.Fee);
            Assert.Equal(transaction.Domain, acr.Domain);

            var infoRequest = new AccountInfoRequest()
            {
                Ledger = new LedgerSpecification(transactionResponse.LedgerIndex.Value),
                Account = account,
            };
            var infoResponse = await Api.AccountInfo(infoRequest);
            Assert.Equal(account, infoResponse.AccountData.Account);
            Assert.Equal(transaction.Domain, infoResponse.AccountData.Domain);
        }

        [Fact]
        public async Task TestSetRegularKey()
        {
            var account = Setup.TestAccountOne.Address;
            var secret = Setup.TestAccountOne.Secret;

            var seed = new Seed("ssKXuaAGcAXaKBf7d532v8KeypdoS");
            var keypair = seed.Ed25519KeyPair();

            var transaction = new SetRegularKey();
            transaction.Account = account;
            transaction.RegularKey = AccountId.FromPublicKey(keypair.GetCanonicalPublicKey());
            await AutofillTransaction(transaction);

            var submitResponse = await SubmitTransaction(secret, transaction, out var transactionHash);

            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var transactionResponse = await WaitForTransaction(transactionHash);
            var srkr = Assert.IsType<SetRegularKey>(transactionResponse.Transaction);
            Assert.Equal(transaction.RegularKey, srkr.RegularKey);

            // Check we can do a noop AccountSet
            var accountSetTransaction = new AccountSet();
            accountSetTransaction.Account = account;
            accountSetTransaction.Sequence = transaction.Sequence + 1;
            accountSetTransaction.Fee = transaction.Fee;

            // Submit with our ed25519 keypair
            var submitRequest = new SubmitRequest();
            submitRequest.TxBlob = accountSetTransaction.Sign(keypair, out transactionHash);
            submitResponse = await Api.Submit(submitRequest);

            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var accountSetTransactionResponse = await WaitForTransaction(transactionHash);
            var acr = Assert.IsType<AccountSet>(accountSetTransactionResponse.Transaction);

            var infoRequest = new AccountInfoRequest()
            {
                Ledger = new LedgerSpecification(transactionResponse.LedgerIndex.Value),
                Account = account,
            };
            var infoResponse = await Api.AccountInfo(infoRequest);
            Assert.Equal(account, infoResponse.AccountData.Account);
        }

        [Fact]
        public async Task TestXrpPayment()
        {
            var accountOne = Setup.TestAccountOne.Address;
            var accountTwo = Setup.TestAccountTwo.Address;
            var secret = Setup.TestAccountOne.Secret;

            ulong startingDrops;
            {
                var response = await Api.AccountInfo(new AccountInfoRequest()
                {
                    Ledger = LedgerSpecification.Current,
                    Account = accountTwo,
                });
                startingDrops = response.AccountData.Balance.Drops;
            }

            var transaction = new Payment();
            transaction.Account = accountOne;
            transaction.Destination = accountTwo;
            transaction.DestinationTag = 1;
            transaction.Amount = new Amount(100);
            await AutofillTransaction(transaction);

            var submitResponse = await SubmitTransaction(secret, transaction, out var transactionHash);

            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var transactionResponse = await WaitForTransaction(transactionHash);
            var pr = Assert.IsType<Payment>(transactionResponse.Transaction);
            Assert.Equal(transaction.Amount, pr.Amount);

            ulong endingDrops;
            {
                var response = await Api.AccountInfo(new AccountInfoRequest()
                {
                    Ledger = LedgerSpecification.Current,
                    Account = accountTwo,
                });
                endingDrops = response.AccountData.Balance.Drops;
            }

            Assert.Equal(100ul, endingDrops - startingDrops);
        }

        [Fact]
        public async Task TestGbpPayment()
        {
            var accountOne = Setup.TestAccountOne.Address;
            var accountTwo = Setup.TestAccountTwo.Address;
            var secretOne = Setup.TestAccountOne.Secret;
            var secretTwo = Setup.TestAccountTwo.Secret;

            // Set up a trust line
            var trustSet = new TrustSet();
            trustSet.Account = accountTwo;
            trustSet.LimitAmount = new IssuedAmount(accountOne, new CurrencyCode("GBP"), new Currency(1000m));
            await AutofillTransaction(trustSet);

            // Submit and wait for the trust line
            var submitResponse = await SubmitTransaction(secretTwo, trustSet, out var transactionHash);
            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);
            var _ = await WaitForTransaction(transactionHash);

            // Send 100GBP
            var transaction = new Payment();
            transaction.Account = accountOne;
            transaction.Destination = accountTwo;
            transaction.DestinationTag = 1;
            transaction.Amount = new Amount(accountOne, new CurrencyCode("GBP"), new Currency(100m));
            await AutofillTransaction(transaction);

            submitResponse = await SubmitTransaction(secretOne, transaction, out transactionHash);
            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var transactionResponse = await WaitForTransaction(transactionHash);
            var pr = Assert.IsType<Payment>(transactionResponse.Transaction);
            Assert.Equal(transaction.Amount, pr.Amount);
            
            // Check we have +100 GBP on our trust line
            var linesRequest = new AccountLinesRequest();
            linesRequest.Account = accountTwo;
            var linesResponse = await Api.AccountLines(linesRequest);
            var lines = new List<TrustLine>();
            await foreach (var line in linesResponse)
            {
                lines.Add(line);
            }
            var trustLine = lines.First();
            Assert.Equal(accountOne, trustLine.Account);
            Assert.Equal(new CurrencyCode("GBP"), trustLine.Currency);
            Assert.Equal(new Currency(100m), trustLine.Balance);
        }
    }
}
