# Ripple API for dotnet

![.NET Core](https://github.com/Ibasa/Ripple/workflows/.NET%20Core/badge.svg?branch=master) [![NuGet latest release](https://img.shields.io/nuget/v/Ibasa.Ripple.svg)](https://www.nuget.org/packages/Ibasa.Ripple)

Ibasa.Ripple is a .NET library for interacting with the Ripple WebSocket and JSON RPC APIs.

## Usage

Get the current base fee for the test net using the websocket api:
```csharp
using Ibasa.Ripple

var clientWebSocket = new ClientWebSocket();
await clientWebSocket.ConnectAsync(new Uri("wss://s.altnet.rippletest.net:51233"), CancellationToken.None);
using(var webSocketApi = new WebSocketApi(clientWebSocket))
{
  var response = webSocketApi.Fee(CancellationToken.None);
  Console.WriteLine("Current test net base fee in drops is: {0}", response.Drops.BaseFee);
}
```

Send 100 ripple on the main net using the json RPC api:
```csharp
using Ibasa.Ripple

// Example account addresses and seeds
var sendingAccount = new AccountId("rJWB6XzACgY17nPbC5TBaxpUFC9GNFxbfs");
var sendingSeed = new Seed("sn9j8Jc9piiGWCXQ1Tisyr1mhFYVV");
var receivingAccount = new AccountId("rEopG7qc7ZWFMvCSH3GYeJJ7GPAQnKmxgw");

var httpClient = new HttpClient();
httpClient.BaseAddress = "https://s1.ripple.com:51234/";
using(var jsonRpcApi = new JsonRpcApi(httpClient))
{
  // Get the sending accounts current sequence number
  var infoRequest = new AccountInfoRequest()
  {
      Ledger = LedgerSpecification.Closed,
      Account = sendingAccount,
  };
  var infoResponse = await Api.AccountInfo(infoRequest);
  // Get the current network fees
  var feeResponse = await Api.Fee();

  // Setup a payment transaction
  var transaction = new Payment();
  transaction.Account = sendingAccount;
  transaction.Sequence = infoResponse.AccountData.Sequence;
  transaction.Fee = feeResponse.Drops.MedianFee;
  transaction.Destination = receivingAccount;
  transaction.Amount = new Amount(100);

  // Locally sign and submit it to the network
  var submitRequest = new SubmitRequest();
  sendingSeed.Secp256k1KeyPair(out var _, out var keyPair);
  submitRequest.TxBlob = transaction.Sign(keyPair, out var transactionHash);
  var submitResponse = await Api.Submit(submitRequest);

  Console.WriteLine("Submitted transaction {0} with result: {1} {2}",
    transactionHash,
    submitResponse.EngineResult, submitResponse.EngineResultMessage);
}
```

## License

This project is licensed under the LGPL License - see the [LICENSE](LICENSE) file for details
