# Ripple API for dotnet

![.NET Core](https://github.com/Ibasa/Ripple/workflows/.NET%20Core/badge.svg?branch=master) [![NuGet latest release](https://img.shields.io/nuget/v/Ibasa.Ripple.svg)](https://www.nuget.org/packages/Ibasa.Ripple)

Ibasa.Ripple is a .NET library for interacting with the Ripple WebSocket and JSON RPC APIs. 

## Usage

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

## License

This project is licensed under the LGPL License - see the [LICENSE](LICENSE) file for details
