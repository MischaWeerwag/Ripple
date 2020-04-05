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

        private async Task<uint> GetAccountSequnce(AccountId account)
        {
            var request = new AccountInfoRequest()
            {
                Ledger = LedgerSpecification.Current,
                Account = account,
            };
            var response = await Api.AccountInfo(request);
            return response.AccountData.Sequence;
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
                if (transactionResponse.LedgerIndex.HasValue)
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
        public async Task TestLedger()
        {
            // Validated with Transactions and Expand = true
            // ValueKind = Object : "{
            // "ledger":{
            //  "accepted":true,
            //  "account_hash":"F7F48754DA517B52A0C9B2B53BAB9B156709D1285E4095FC6625AD4257D2AA42",
            //  "close_flags":0,
            //  "close_time":639266720,
            //  "close_time_human":"2020-Apr-03 22:05:20.000000000 UTC",
            //  "close_time_resolution":10,
            //  "closed":true,
            //  "hash":"B755FD8F777E3F9332FF4EFDE0B6EFA145DA5E78C2323259C5ADC5C639675C3B",
            //  "ledger_hash":"B755FD8F777E3F9332FF4EFDE0B6EFA145DA5E78C2323259C5ADC5C639675C3B",
            //  "ledger_index":"5989828",
            //  "parent_close_time":639266712,
            //  "parent_hash":"36403CFE94DB54F0DDE7E288BBFCCE7B62AB435E97096B0216076DCBE9CAFAB8",
            //  "seqNum":"5989828",
            //  "totalCoins":"99999972797423393",
            //  "total_coins":"99999972797423393",
            //  "transaction_hash":"5583C9AD7F848E1D8D96323D243CF1D8CAD1391F76011E92461F81F5F287FD16",
            //  "transactions":[
            //      {"Account":"rPm88mdDuXLgxzpmZPXf6wPQ1ZTHRNvYVr","Amount":"20000000","Destination":"rDJFnv5sEfp42LMFiX3mVQKczpFTdxYDzM","Fee":"12","Flags":2147483648,"LastLedgerSequence":5989829,"Sequence":9358785,"SigningPubKey":"02A61C710649C858A03DF50C8D24563613FC4D905B141EEBE019364675929AB804","TransactionType":"Payment","TxnSignature":"3045022100DC4675501C0EC32AF7037F023A0D356660A11C448847D99062689B24DF2B1A84022041925AA589D66C02A444BFC7221F4FBB55FD41EEE34079D4B6C4467FC872B921","hash":"3A8BC87E8D1596C876A60B152A066F63E8F87375AA6AF380C548890332AA942B","metaData":{"AffectedNodes":[{"ModifiedNode":{"FinalFields":{"Account":"rDJFnv5sEfp42LMFiX3mVQKczpFTdxYDzM","Balance":"25872341347","Flags":0,"OwnerCount":0,"Sequence":9357990},"LedgerEntryType":"AccountRoot","LedgerIndex":"31794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1F","PreviousFields":{"Balance":"25852341347"},"PreviousTxnID":"DA5DCDA83A4C93D32DFC32E1538D16F4A7FB56DE0D6248395C2FB1C3D29E5349","PreviousTxnLgrSeq":5989828}},{"ModifiedNode":{"FinalFields":{"Account":"rPm88mdDuXLgxzpmZPXf6wPQ1ZTHRNvYVr","Balance":"43806320","Flags":0,"OwnerCount":0,"Sequence":9358786},"LedgerEntryType":"AccountRoot","LedgerIndex":"8EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FF","PreviousFields":{"Balance":"63806332","Sequence":9358785},"PreviousTxnID":"DA5DCDA83A4C93D32DFC32E1538D16F4A7FB56DE0D6248395C2FB1C3D29E5349","PreviousTxnLgrSeq":5989828}}],"TransactionIndex":3,"TransactionResult":"tesSUCCESS","delivered_amount":"20000000"}},{"Account":"rQwkvgwZnFnY6pQFhdy5KhSvRSPfztsJvM","Amount":"1","Destination":"rhQUNZnQDtDGeHiX5RmMMSW3mvYXBHBMrT","Fee":"12","InvoiceID":"D962858C52DB00EAE406E3070FD06E19E17BCB0C2CF84D157067FDE6089BA69A","Sequence":5740,"SigningPubKey":"0258BD9219D58D0F4AA04714902ACA1AC3D08028DBDC94AC024E1938EC6273A723","TransactionType":"Payment","TxnSignature":"3045022100BD4E9F1194875E4BED60A7D4F8DF66DDA1C82F52DB06F37C6BA2774B44837E0D022064BADD37DBE673310522DA85966E366A890FC1E111E0EFC020528B1F01FAC39A","hash":"3D1E1003B0444E89E5FD323349D903C7DF5EF8A1153BF7898D2FECD781D4BF88","metaData":{"AffectedNodes":[{"ModifiedNode":{"FinalFields":{"Account":"rhQUNZnQDtDGeHiX5RmMMSW3mvYXBHBMrT","Balance":"21002054","Flags":0,"OwnerCount":0,"Sequence":2},"LedgerEntryType":"AccountRoot","LedgerIndex":"5F0ED604BA81210F59C00CE87C918AB45485B686EE04EA77695B586FA7174379","PreviousFields":{"Balance":"21002053"},"PreviousTxnID":"716446E5ADA818EE0B8AEF1715C1644E1383E1FDE7EF34486641AEE06B24605A","PreviousTxnLgrSeq":5989542}},{"ModifiedNode":{"FinalFields":{"Account":"rQwkvgwZnFnY6pQFhdy5KhSvRSPfztsJvM","Balance":"1978929054","Flags":0,"OwnerCount":0,"Sequence":5741},"LedgerEntryType":"AccountRoot","LedgerIndex":"EC649771E54DF6C6C7743A7C1BB3B9B3033D06575C0C0A21C7B7DD0307F83737","PreviousFields":{"Balance":"1978929067","Sequence":5740},"PreviousTxnID":"716446E5ADA818EE0B8AEF1715C1644E1383E1FDE7EF34486641AEE06B24605A","PreviousTxnLgrSeq":5989542}}],"TransactionIndex":0,"TransactionResult":"tesSUCCESS","delivered_amount":"1"}},{"Account":"rDJFnv5sEfp42LMFiX3mVQKczpFTdxYDzM","Amount":"20000000","Destination":"rPm88mdDuXLgxzpmZPXf6wPQ1ZTHRNvYVr","Fee":"12","Flags":2147483648,"LastLedgerSequence":5989829,"Sequence":9357988,"SigningPubKey":"02E6CB923A531044CB194A2F7477B38C6D2B499FA67FFC38203CEADC7D8A7DFF54","TransactionType":"Payment","TxnSignature":"3045022100CA2F57B8F7796BE92AD8E75DCB625523404BD706F846B196837C346D6D66C3B602201AA81A88A265930AAC323F118913C3589F53E44196A3A8FC3721B7D949BD4958","hash":"6DEA6BEAFB0F3D36F960AEBCFA6869DE26A3D8BA9C92237593EB1D9FDF62C0D1","metaData":{"AffectedNodes":[{"ModifiedNode":{"FinalFields":{"Account":"rDJFnv5sEfp42LMFiX3mVQKczpFTdxYDzM","Balance":"25872341359","Flags":0,"OwnerCount":0,"Sequence":9357989},"LedgerEntryType":"AccountRoot","LedgerIndex":"31794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1F","PreviousFields":{"Balance":"25892341371","Sequence":9357988},"PreviousTxnID":"9C8C0D87093C5B117B3D3CFB6CE11E23017519EAD19D341A022470A2FCBE2983","PreviousTxnLgrSeq":5989827}},{"ModifiedNode":{"FinalFields":{"Account":"rPm88mdDuXLgxzpmZPXf6wPQ1ZTHRNvYVr","Balance":"43806332","Flags":0,"OwnerCount":0,"Sequence":9358785},"LedgerEntryType":"AccountRoot","LedgerIndex":"8EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FF","PreviousFields":{"Balance":"23806332"},"PreviousTxnID":"9C8C0D87093C5B117B3D3CFB6CE11E23017519EAD19D341A022470A2FCBE2983","PreviousTxnLgrSeq":5989827}}],"TransactionIndex":1,"TransactionResult":"tesSUCCESS","delivered_amount":"20000000"}},{"Account":"rDJFnv5sEfp42LMFiX3mVQKczpFTdxYDzM","Amount":"20000000","Destination":"rPm88mdDuXLgxzpmZPXf6wPQ1ZTHRNvYVr","Fee":"12","Flags":2147483648,"LastLedgerSequence":5989829,"Sequence":9357989,"SigningPubKey":"02E6CB923A531044CB194A2F7477B38C6D2B499FA67FFC38203CEADC7D8A7DFF54","TransactionType":"Payment","TxnSignature":"3045022100C609B351DDD3A3EFB6849F7A64722729A39D4BF44EC127C668C206A4A1DE0032022040A00B6EFFB74F8AD6D8522B5B6FE2BC0186396984C1C673B0FD772214F70191","hash":"DA5DCDA83A4C93D32DFC32E1538D16F4A7FB56DE0D6248395C2FB1C3D29E5349","metaData":{"AffectedNodes":[{"ModifiedNode":{"FinalFields":{"Account":"rDJFnv5sEfp42LMFiX3mVQKczpFTdxYDzM","Balance":"25852341347","Flags":0,"OwnerCount":0,"Sequence":9357990},"LedgerEntryType":"AccountRoot","LedgerIndex":"31794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1F","PreviousFields":{"Balance":"25872341359","Sequence":9357989},"PreviousTxnID":"6DEA6BEAFB0F3D36F960AEBCFA6869DE26A3D8BA9C92237593EB1D9FDF62C0D1","PreviousTxnLgrSeq":5989828}},{"ModifiedNode":{"FinalFields":{"Account":"rPm88mdDuXLgxzpmZPXf6wPQ1ZTHRNvYVr","Balance":"63806332","Flags":0,"OwnerCount":0,"Sequence":9358785},"LedgerEntryType":"AccountRoot","LedgerIndex":"8EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FF","PreviousFields":{"Balance":"43806332"},"PreviousTxnID":"6DEA6BEAFB0F3D36F960AEBCFA6869DE26A3D8BA9C92237593EB1D9FDF62C0D1","PreviousTxnLgrSeq":5989828}}],"TransactionIndex":2,"TransactionResult":"tesSUCCESS","delivered_amount":"20000000"}}]},
            // "ledger_hash":"B755FD8F777E3F9332FF4EFDE0B6EFA145DA5E78C2323259C5ADC5C639675C3B",
            // "ledger_index":5989828,
            // "validated":true}"


            // in binary
            // ValueKind = Object : "{
            //  "ledger":{
            // "closed":true,
            // "ledger_data":"005B67A60163457208228EB931635D1A225FF646885BFA8A33A2A3424BFE3290E493C6C6CEB9A51D84D5457BD96702F77340ED9C5294B88A13A0F42BE487D7921F18A2916E6BCA9AC3F9D3F1EEBDADA11F85DA6AB50E29754BEBF4276C327BA05E48004AE574C8F04B4493FD261A7568261A75690A00",
            // "transactions":[
            //      {"meta":"201C00000001F8E511006125005B67A655A7875D031A4A29AD5AEF5D299115407F242A6CE42B46F44F871127F0CA5818155631794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1FE624008ECD8B6240000006061C76A7E1E7220000000024008ECD8C2D00000000624000000604EB499B811486FFE2A17E861BA0FE9A3ED8352F895D80E789E0E1E1E511006125005B67A655A7875D031A4A29AD5AEF5D299115407F242A6CE42B46F44F871127F0CA581815568EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FFE66240000000029C4BB4E1E7220000000024008ED0A72D00000000624000000003CD78B48114F9CB4ADAC227928306BDC5E679C485D4A676BFB8E1E1F1031000",
            //      "tx_blob":"120000228000000024008ECD8B201B005B67A7614000000001312D0068400000000000000C732102E6CB923A531044CB194A2F7477B38C6D2B499FA67FFC38203CEADC7D8A7DFF547446304402206A3281436FDEF9B5F8AF377FA22377BB7F87AA56C95D0772ECD1C6974CACD10002205AAFBD0645B149AD1907E478BC24FB77FADE2D210F7D0C384D1D6CD8D82EE212811486FFE2A17E861BA0FE9A3ED8352F895D80E789E08314F9CB4ADAC227928306BDC5E679C485D4A676BFB8"},
            //      {"meta":"201C00000002F8E511006125005B67A6550370694CBE0D8BC59A02360B4276595331089E31E95D04691ACF645E36507B175631794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1FE6624000000604EB499BE1E7220000000024008ECD8C2D000000006240000006061C769B811486FFE2A17E861BA0FE9A3ED8352F895D80E789E0E1E1E511006125005B67A6550370694CBE0D8BC59A02360B4276595331089E31E95D04691ACF645E36507B17568EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FFE624008ED0A7624000000003CD78B4E1E7220000000024008ED0A82D000000006240000000029C4BA88114F9CB4ADAC227928306BDC5E679C485D4A676BFB8E1E1F1031000",
            //      "tx_blob":"120000228000000024008ED0A7201B005B67A7614000000001312D0068400000000000000C732102A61C710649C858A03DF50C8D24563613FC4D905B141EEBE019364675929AB80474473045022100F44B5CB327C544865C089386F2632AD65AEC7D37B9896779AD0806C90188635E02207F4CB501EDF9289EF83B81178CF787D0034E49CEFD99134E1EFD1097CC64D7F58114F9CB4ADAC227928306BDC5E679C485D4A676BFB8831486FFE2A17E861BA0FE9A3ED8352F895D80E789E0"},
            //      {"meta":"201C00000000F8E511006125005B67A555EE1CDCA5FBFD5C0AE556BEBAA3B80C13CD5BCB2EC60328F42DEE56AB56BECCCD5631794F29F9E987DC45A7997416503E0E3A5C0D114B050845B76F2D9D9FF9DC1FE624008ECD8A6240000006074DA3B3E1E7220000000024008ECD8B2D000000006240000006061C76A7811486FFE2A17E861BA0FE9A3ED8352F895D80E789E0E1E1E511006125005B67A555EE1CDCA5FBFD5C0AE556BEBAA3B80C13CD5BCB2EC60328F42DEE56AB56BECCCD568EEC72369A874DEC57AC3C11F40714D79D56F0079AA1948B38CC044D3F6F79FFE66240000000016B1EB4E1E7220000000024008ED0A72D000000006240000000029C4BB48114F9CB4ADAC227928306BDC5E679C485D4A676BFB8E1E1F1031000",
            //      "tx_blob":"120000228000000024008ECD8A201B005B67A7614000000001312D0068400000000000000C732102E6CB923A531044CB194A2F7477B38C6D2B499FA67FFC38203CEADC7D8A7DFF547447304502210084915DC8B428B910782977809BA0F58D4BBE29E00136DE3E30B2C128097D0A01022026517FEDDD9F1A90B83042154353E34F952BDAE891FDBF28C3B9D2986FD9A531811486FFE2A17E861BA0FE9A3ED8352F895D80E789E08314F9CB4ADAC227928306BDC5E679C485D4A676BFB8"}]},
            // "ledger_hash":"C9207170D2B28849781BC115B13449DE320E654AD821A0A22635B4620C19BD5C",
            // "ledger_index":5990310,
            // "validated":true}"


            var request = new LedgerRequest();
            request.Ledger = LedgerSpecification.Validated;
            request.Transactions = true;
            request.Expand = true;
            var response = await Api.Ledger(request);

            Assert.NotEqual(default, response.LedgerHash);
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
            var feeResponse = await Api.Fee();

            var transaction = new AccountSet();
            transaction.Account = account;
            transaction.Sequence = await GetAccountSequnce(account);
            transaction.Fee = feeResponse.Drops.MedianFee;
            transaction.Domain = System.Text.Encoding.ASCII.GetBytes("example.com");

            var submitResponse = await SubmitTransaction(secret, transaction, out var transactionHash);

            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var transactionResponse = await WaitForTransaction(transactionHash);
            var acr = Assert.IsType<AccountSetResponse>(transactionResponse);
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

            var feeResponse = await Api.Fee();

            var transaction = new Payment();
            transaction.Account = accountOne;
            transaction.Sequence = await GetAccountSequnce(accountOne);
            transaction.Fee = feeResponse.Drops.MedianFee;
            transaction.Destination = accountTwo;
            transaction.DestinationTag = 1;
            transaction.Amount = new Amount(100);

            var submitResponse = await SubmitTransaction(secret, transaction, out var transactionHash);

            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var transactionResponse = await WaitForTransaction(transactionHash);
            var pr = Assert.IsType<PaymentResponse>(transactionResponse);
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
            var feeResponse = await Api.Fee();

            // Set up a trust line
            var trustSet = new TrustSet();
            trustSet.Account = accountTwo;
            trustSet.Sequence = await GetAccountSequnce(accountTwo);
            trustSet.Fee = feeResponse.Drops.MedianFee;
            trustSet.LimitAmount = new IssuedAmount(accountOne, new CurrencyCode("GBP"), new Currency(1000m));

            // Submit and wait for the trust line
            var submitResponse = await SubmitTransaction(secretTwo, trustSet, out var transactionHash);
            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);
            var _ = await WaitForTransaction(transactionHash);

            // Send 100GBP
            var transaction = new Payment();
            transaction.Account = accountOne;
            transaction.Sequence = await GetAccountSequnce(accountOne);
            transaction.Fee = feeResponse.Drops.MedianFee;
            transaction.Destination = accountTwo;
            transaction.DestinationTag = 1;
            transaction.Amount = new Amount(accountOne, new CurrencyCode("GBP"), new Currency(100m));

            submitResponse = await SubmitTransaction(secretOne, transaction, out transactionHash);
            Assert.Equal(EngineResult.tesSUCCESS, submitResponse.EngineResult);

            var transactionResponse = await WaitForTransaction(transactionHash);
            var pr = Assert.IsType<PaymentResponse>(transactionResponse);
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
