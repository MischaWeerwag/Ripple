using Ibasa.Ripple;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Example
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Create the network client
            var address = new Uri("https://s.altnet.rippletest.net:51234");
            var httpClient = new HttpClient();
            httpClient.BaseAddress = address;
            var xrplClient = new JsonRpcApi(httpClient);

            // Create a wallet using the testnet faucet
            var faucetClient = new FaucetClient();
            var testSeed = await faucetClient.Generate();
            Console.WriteLine(testSeed);

            // Create an account string from the wallet
            // N.B rootKeyPair will be null for ED25519 keys
            testSeed.GetKeyPairs(out var rootKeyPair, out var keyPair);
            var accountId = AccountId.FromPublicKey(keyPair.PublicKey.GetCanoncialBytes());
            Console.WriteLine(accountId);

            // Look up info about your account, need to do this in a loop because it will take some time for the account to actually be present in a validated ledger
            while (true)
            {
                var infoRequest = new AccountInfoRequest()
                {
                    Account = accountId,
                    Ledger = LedgerSpecification.Validated
                };
                try
                {
                    var infoResponse = await xrplClient.AccountInfo(infoRequest);
                    Console.WriteLine("Balance: {0}", infoResponse.AccountData.Balance);
                    break;
                }
                catch (RippleRequestException exc)
                {
                    if (exc.Error == "actNotFound") continue;
                    throw;
                }
            }
        }
    }
}