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

    public abstract class ApiTestsSetup<T> : IDisposable where T : Api
    {
        public readonly TestAccount TestAccountOne;
        public readonly TestAccount TestAccountTwo;

        public readonly T Api;

        protected abstract T CreateApi();

        public ApiTestsSetup()
        {
            TestAccountOne = TestAccount.Create();
            TestAccountTwo = TestAccount.Create();
            Api = CreateApi();

            // Wait for the two accounts from setup to exists
            WaitForAccount(TestAccountOne.Address).Wait();
            WaitForAccount(TestAccountOne.Address).Wait();
        }

        /// <summary>
        /// Wait for the account to exist in a validated ledger.
        /// </summary>
        public async Task<AccountInfoResponse> WaitForAccount(AccountId account)
        {
            var infoRequest = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Validated,
                Account = account,
            };
            AccountInfoResponse infoResponse = null;
            while (infoResponse == null)
            {
                try
                {
                    infoResponse = await Api.AccountInfo(infoRequest);
                }
                catch (RippleRequestException exc)
                {
                    if (exc.Error != "actNotFound") { throw; }
                }
            }
            return infoResponse;
        }


        public void Dispose()
        {
            Api.DisposeAsync().AsTask().Wait();
        }
    }

    public abstract class ApiTests<T> where T : Api
    {
        protected readonly ApiTestsSetup<T> Setup;
        protected T Api { get { return Setup.Api; } }

        /// <summary>
        /// We always submit a single transaction and then wait for it to be validated before continuing with the tests.
        /// This is a little slow (ledgers only validate every 4s or so), but it keeps test logic simple.
        /// </summary>
        private async Task<Tuple<SubmitResponse, TransactionResponse>> SubmitTransaction(Seed secret, Transaction transaction)
        {
            AccountInfoResponse infoResponse = await Setup.WaitForAccount(transaction.Account);

            var feeResponse = await Api.Fee();

            transaction.LastLedgerSequence = feeResponse.LedgerCurrentIndex + 8;
            transaction.Sequence = infoResponse.AccountData.Sequence;
            transaction.Fee = feeResponse.Drops.MedianFee;

            secret.KeyPair(out var _, out var keyPair);

            var request = new SubmitRequest();
            request.FailHard = true;
            request.TxBlob = transaction.Sign(keyPair, out var transactionHash);
            var submitResponse = await Api.Submit(request);
            if (!submitResponse.Accepted)
            {
                throw new Exception(string.Format("Transaction {0} was not accepted", transactionHash));
            }

            var txRequest = new TxRequest()
            {
                Transaction = transactionHash,
                MinLedger = feeResponse.LedgerCurrentIndex,
                MaxLedger = transaction.LastLedgerSequence,
            };
            while (true)
            {
                var ledgerCurrent = await Api.LedgerCurrent();
                var transactionResponse = await Api.Tx(txRequest);
                Assert.Equal(transactionHash, transactionResponse.Hash);
                if (transactionResponse.Validated)
                {
                    return Tuple.Create(submitResponse, transactionResponse);
                }

                if (ledgerCurrent > txRequest.MaxLedger)
                {
                    throw new Exception(string.Format("Never got transaction {0} validated", transactionHash));
                }

                System.Threading.Thread.Sleep(2000);
            }
        }

        public ApiTests(ApiTestsSetup<T> setup)
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
            var closed = await Api.LedgerClosed();
            var current = await Api.LedgerCurrent();

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
            foreach (var tx in response.Transactions)
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
            Assert.False(response.Validated);
            Assert.Equal(account, response.AccountData.Account);
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

            var (_, transactionResponse) = await SubmitTransaction(secret, transaction);
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
            seed.KeyPair(out var _, out var keyPair);

            var transaction = new SetRegularKey();
            transaction.Account = account;
            transaction.RegularKey = AccountId.FromPublicKey(keyPair.GetCanonicalPublicKey());

            var (_, transactionResponse) = await SubmitTransaction(secret, transaction);
            var srkr = Assert.IsType<SetRegularKey>(transactionResponse.Transaction);
            Assert.Equal(transaction.RegularKey, srkr.RegularKey);

            // Check we can do a noop AccountSet
            var accountSetTransaction = new AccountSet();
            accountSetTransaction.Account = account;
            accountSetTransaction.Sequence = transaction.Sequence + 1;
            accountSetTransaction.Fee = transaction.Fee;

            // Submit with our ed25519 keypair
            var (_, accountSetTransactionResponse) = await SubmitTransaction(seed, accountSetTransaction);
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

            var (_, transactionResponse) = await SubmitTransaction(secret, transaction);
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

            // Submit and wait for the trust line
            var (_, _) = await SubmitTransaction(secretTwo, trustSet);

            // Send 100GBP
            var payment = new Payment();
            payment.Account = accountOne;
            payment.Destination = accountTwo;
            payment.DestinationTag = 1;
            payment.Amount = new Amount(accountOne, new CurrencyCode("GBP"), new Currency(100m));

            var (_, transactionResponse) = await SubmitTransaction(secretOne, payment);
            var pr = Assert.IsType<Payment>(transactionResponse.Transaction);
            Assert.Equal(payment.Amount, pr.Amount);

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

            var request = new AccountCurrenciesRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = accountOne,
            };
            var currencies = await Api.AccountCurrencies(request);
            Assert.Equal(new CurrencyCode("GBP"), Assert.Single(currencies.SendCurrencies));
            Assert.Equal(new CurrencyCode("GBP"), Assert.Single(currencies.ReceiveCurrencies));
        }

        [Fact]
        public async Task TestNoRipple()
        {
            // We need a fresh account setup for this test
            var testAccount = TestAccount.Create();
            await Setup.WaitForAccount(testAccount.Address);

            // All tests will be against this new account,
            // also all but the last will have Transactions set true.
            var request = new NoRippleCheckRequest();
            request.Ledger = LedgerSpecification.Current;
            request.Account = testAccount.Address;
            request.Transactions = true;

            // No trust lines yet, check that the user has no problems
            {
                request.Role = "user";
                var response = await Api.NoRippleCheck(request);
                Assert.NotEqual(default, response.LedgerCurrentIndex);
                Assert.Empty(response.Problems);
                Assert.Empty(response.Transactions);
            }

            // No trust lines yet, check that the gateway has one problem to set the default ripple flag
            {
                request.Role = "gateway";
                var response = await Api.NoRippleCheck(request);
                Assert.NotEqual(default, response.LedgerCurrentIndex);
                Assert.Equal("You should immediately set your default ripple flag", Assert.Single(response.Problems));
                var transaction = Assert.Single(response.Transactions);
                transaction.Account = testAccount.Address;
                var accountSet = Assert.IsType<AccountSet>(transaction);
                Assert.Equal(AccountSetFlags.DefaultRipple, accountSet.SetFlag);
            }

            // Make a GBP trust line to account 1
            {
                var trustSet = new TrustSet();
                trustSet.Account = testAccount.Address;
                trustSet.LimitAmount = new IssuedAmount(Setup.TestAccountOne.Address, new CurrencyCode("GBP"), new Currency(1000m));

                // Submit and wait for the trust line
                var (_, _) = await SubmitTransaction(testAccount.Secret, trustSet);
            }

            // Trust lines setup, check that the user has one problem
            {
                request.Role = "user";
                var response = await Api.NoRippleCheck(request);
                Assert.NotEqual(default, response.LedgerCurrentIndex);
                var expected = "You should probably set the no ripple flag on your GBP line to " + Setup.TestAccountOne.Address.ToString();
                Assert.Equal(expected, Assert.Single(response.Problems));
                var transaction = Assert.Single(response.Transactions);
                transaction.Account = testAccount.Address;
                var trustSet = Assert.IsType<TrustSet>(transaction);
                Assert.Equal(Setup.TestAccountOne.Address, trustSet.LimitAmount.Issuer);
                Assert.Equal(new CurrencyCode("GBP"), trustSet.LimitAmount.CurrencyCode);
                Assert.Equal(new Currency(1000m), trustSet.LimitAmount.Value);
                Assert.Equal(TrustFlags.SetNoRipple, trustSet.Flags);
            }

            // Trust lines setup, check that the gateway has one problem to set the default ripple flag
            {
                // This is the last request we make that COULD return transactions so check that if this is set false we don't see any
                request.Transactions = false;
                request.Role = "gateway";
                var response = await Api.NoRippleCheck(request);
                Assert.NotEqual(default, response.LedgerCurrentIndex);
                Assert.Equal("You should immediately set your default ripple flag", Assert.Single(response.Problems));
                // Empty because request.Transactions = false
                Assert.Empty(response.Transactions);
            }

            // Set the no ripple flag and make sure gateway now returns no issues
            {
                var accountSet = new AccountSet();
                accountSet.Account = testAccount.Address;
                accountSet.SetFlag = AccountSetFlags.DefaultRipple;

                // Submit and wait for the flag set
                var (_, _) = await SubmitTransaction(testAccount.Secret, accountSet);

                var response = await Api.NoRippleCheck(request);
                Assert.NotEqual(default, response.LedgerCurrentIndex);
                Assert.Empty(response.Problems);
                Assert.Empty(response.Transactions);
            }
        }
    }
}
