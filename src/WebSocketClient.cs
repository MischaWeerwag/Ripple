using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    public sealed class RippleException : System.Exception
    {
        public System.Text.Json.JsonElement Request { get; private set; }
        public RippleException(string error, System.Text.Json.JsonElement request) 
            : base(error) 
        {
            Request = request;
        }
    }


    public sealed class WebSocketClient : IDisposable
    {
        private readonly WebSocket socket;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly System.Collections.Generic.Dictionary<int, TaskCompletionSource<System.Text.Json.JsonElement>> responses;
        private int currentId = 0;

        private async void ReceiveLoop()
        {
            var response = new System.IO.MemoryStream();
            var buffer = new byte[1024];
            while(!cancellationTokenSource.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token); 
                response.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    response.Seek(0, System.IO.SeekOrigin.Begin);
                    var json = System.Text.Json.JsonDocument.Parse(response);
                    response.SetLength(0);

                    var type = json.RootElement.GetProperty("type").GetString();
                    if (type == "response")
                    {
                        var id = json.RootElement.GetProperty("id").GetInt32();
                        var status = json.RootElement.GetProperty("status").GetString();
                        if (status == "success")
                        {
                            lock (responses)
                            {
                                if (responses.TryGetValue(id, out var task))
                                {
                                    responses.Remove(id);
                                    task.SetResult(json.RootElement.GetProperty("result"));
                                }
                            }
                        } 
                        else if(status == "error")
                        {
                            var error = json.RootElement.GetProperty("error").GetString();
                            var exception = new RippleException(error, json.RootElement.GetProperty("request"));

                            lock (responses)
                            {
                                if (responses.TryGetValue(id, out var task))
                                {
                                    responses.Remove(id);
                                    task.SetException(exception);
                                }
                            }
                        }
                    }
                }
            }
        }
        
        public WebSocketClient(WebSocket webSocket)
        {
            socket = webSocket;
            cancellationTokenSource = new CancellationTokenSource();
            responses = new System.Collections.Generic.Dictionary<int, TaskCompletionSource<System.Text.Json.JsonElement>>();

            ReceiveLoop();
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            socket.Dispose();
        }

        private async Task<System.Text.Json.JsonElement> ReceiveAsync(int id, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<System.Text.Json.JsonElement>();
            lock(responses)
            {
                responses.Add(id, tcs);
            }
            return await tcs.Task;
        }

        /// <summary>
        /// The ping command returns an acknowledgement, so that clients can test the connection status and latency.
        /// </summary>
        public async Task Ping(CancellationToken cancellationToken = default)
        {
            var buffer = new System.IO.MemoryStream();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = System.Threading.Interlocked.Increment(ref currentId);
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ping");
                writer.WriteEndObject();
            }
            await socket.SendAsync(new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), WebSocketMessageType.Text, true, cancellationToken);
            // Ping just returns an empty object {}
            var _ = await ReceiveAsync(thisId, cancellationToken);
        }

        /// <summary>
        /// The random command provides a random number to be used as a source of entropy for random number generation by clients.
        /// </summary>
        /// <returns>Random 256-bit hex value.</returns>
        public async Task<Hash256> Random(CancellationToken cancellationToken = default)
        {
            var buffer = new System.IO.MemoryStream();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = System.Threading.Interlocked.Increment(ref currentId);
            using (var writer = new System.Text.Json.Utf8JsonWriter(buffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "random");
                writer.WriteEndObject();
            }
            await socket.SendAsync(new ArraySegment<byte>(buffer.GetBuffer(), 0, (int)buffer.Length), WebSocketMessageType.Text, true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new Hash256(response.GetProperty("random").GetString());
        }

        public async Task<LedgerResponse> Ledger(LedgerRequest request = default, CancellationToken cancellationToken = default)
        {
            var stream = new System.IO.MemoryStream();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = System.Threading.Interlocked.Increment(ref currentId);
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ledger");
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteBoolean("full", request.Full);
                writer.WriteBoolean("accounts", request.Accounts);
                writer.WriteBoolean("transactions", request.Transactions);
                writer.WriteBoolean("expand", request.Expand);
                writer.WriteBoolean("owner_funds", request.OwnerFunds);
                writer.WriteBoolean("queue", request.Queue);
                writer.WriteEndObject();
            }

            var buffer = new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);            
            return new LedgerResponse(response);
        }

        public async Task<LedgerClosedResponse> LedgerClosed(CancellationToken cancellationToken = default)
        {
            var stream = new System.IO.MemoryStream();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = System.Threading.Interlocked.Increment(ref currentId);
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ledger_closed");
                writer.WriteEndObject();
            }

            var buffer = new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new LedgerClosedResponse(response);
        }

        public async Task<uint> LedgerCurrent(CancellationToken cancellationToken = default)
        {
            var stream = new System.IO.MemoryStream();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = System.Threading.Interlocked.Increment(ref currentId);
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ledger_current");
                writer.WriteEndObject();
            }

            var buffer = new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return response.GetProperty("ledger_current_index").GetUInt32();
        }

        /// <summary>
        /// The fee command reports the current state of the open-ledger requirements for the transaction cost.
        /// </summary>
        public async Task<FeeResponse> Fee(CancellationToken cancellationToken = default)
        {
            var stream = new System.IO.MemoryStream();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = System.Threading.Interlocked.Increment(ref currentId);
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "fee");
                writer.WriteEndObject();
            }

            var buffer = new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new FeeResponse(response);
        }

        /// <summary>
        /// The account_info command retrieves information about an account, its activity, and its XRP balance. 
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public async Task<AccountInfoResponse> AccountInfo(AccountInfoRequest request = default, CancellationToken cancellationToken = default)
        {
            var stream = new System.IO.MemoryStream();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = System.Threading.Interlocked.Increment(ref currentId);
            using (var writer = new System.Text.Json.Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "account_info");
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account);
                writer.WriteBoolean("strict", request.Strict);
                writer.WriteBoolean("queue", request.Queue);
                writer.WriteBoolean("signer_lists", request.SignerLists);
                writer.WriteEndObject();
            }

            var buffer = new ArraySegment<byte>(stream.GetBuffer(), 0, (int)stream.Length);
            await socket.SendAsync(buffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new AccountInfoResponse(response);
        }
    }
}
