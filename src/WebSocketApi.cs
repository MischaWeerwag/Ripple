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


    public sealed class WebSocketApi : IDisposable
    {
        private readonly ClientWebSocket socket;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly System.Collections.Generic.Dictionary<uint, TaskCompletionSource<System.Text.Json.JsonElement>> responses;
        private readonly Task receiveTask;
        private readonly System.Buffers.ArrayBufferWriter<byte> jsonBuffer = new System.Buffers.ArrayBufferWriter<byte>();
        private uint currentId = 0U;

        private async Task ReceiveLoop()
        {
            var response = new System.Buffers.ArrayBufferWriter<byte>();
            while(!cancellationTokenSource.IsCancellationRequested)
            {
                var buffer = response.GetMemory();
                try
                {
                    var result = await socket.ReceiveAsync(buffer, cancellationTokenSource.Token);
                    response.Advance(result.Count);
                    if (result.EndOfMessage)
                    {
                        var json = System.Text.Json.JsonDocument.Parse(response.WrittenMemory);

                        var type = json.RootElement.GetProperty("type").GetString();
                        if (type == "response")
                        {
                            var id = json.RootElement.GetProperty("id").GetUInt32();
                            var status = json.RootElement.GetProperty("status").GetString();
                            if (status == "success")
                            {
                                lock (responses)
                                {
                                    if (responses.TryGetValue(id, out var task))
                                    {
                                        responses.Remove(id);
                                        task.SetResult(json.RootElement.GetProperty("result").Clone());
                                    }
                                }
                            }
                            else if (status == "error")
                            {
                                var error = json.RootElement.GetProperty("error").GetString();
                                var exception = new RippleException(error, json.RootElement.GetProperty("request").Clone());

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

                        response.Clear();
                    }
                }
                catch(TaskCanceledException taskCanceledException)
                {
                    if(taskCanceledException.CancellationToken == cancellationTokenSource.Token)
                    {
                        // We canceled the receive, while loop will now terminate and task completes successfully
                    }
                    else
                    {
                        // Something else unexpected was cancelled, rethrow
                        throw;
                    }
                }
            }
            socket.Dispose();
        }
        
        public WebSocketApi(ClientWebSocket clientWebSocket)
        {
            socket = clientWebSocket;
            cancellationTokenSource = new CancellationTokenSource();
            responses = new System.Collections.Generic.Dictionary<uint, TaskCompletionSource<System.Text.Json.JsonElement>>();
            receiveTask = ReceiveLoop();
        }

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            receiveTask.Wait();
        }

        private async Task<System.Text.Json.JsonElement> ReceiveAsync(uint id, CancellationToken cancellationToken)
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
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ping");
                writer.WriteEndObject();
            }
            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, true, cancellationToken);
            // Ping just returns an empty object {}
            var _ = await ReceiveAsync(thisId, cancellationToken);
        }

        /// <summary>
        /// The random command provides a random number to be used as a source of entropy for random number generation by clients.
        /// </summary>
        /// <returns>Random 256-bit hex value.</returns>
        public async Task<Hash256> Random(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "random");
                writer.WriteEndObject();
            }
            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new Hash256(response.GetProperty("random").GetString());
        }

        public async Task<LedgerResponse> Ledger(LedgerRequest request = default, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
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

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);            
            return new LedgerResponse(response);
        }

        public async Task<LedgerClosedResponse> LedgerClosed(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ledger_closed");
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new LedgerClosedResponse(response);
        }

        public async Task<uint> LedgerCurrent(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ledger_current");
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return response.GetProperty("ledger_current_index").GetUInt32();
        }

        /// <summary>
        /// The fee command reports the current state of the open-ledger requirements for the transaction cost.
        /// </summary>
        public async Task<FeeResponse> Fee(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "fee");
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new FeeResponse(response);
        }

        /// <summary>
        /// The account_info command retrieves information about an account, its activity, and its XRP balance. 
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public async Task<AccountInfoResponse> AccountInfo(AccountInfoRequest request = default, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "account_info");
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account.ToString());
                writer.WriteBoolean("strict", request.Strict);
                writer.WriteBoolean("queue", request.Queue);
                writer.WriteBoolean("signer_lists", request.SignerLists);
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new AccountInfoResponse(response);
        }

        /// <summary>
        /// The account_currencies command retrieves a list of currencies that an account can send or receive, based on its trust lines. (This is not a thoroughly confirmed list, but it can be used to populate user interfaces.)
        /// </summary>
        public async Task<AccountCurrenciesResponse> AccountCurrencies(AccountCurrenciesRequest request = default, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "account_currencies");
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account.ToString());
                writer.WriteBoolean("strict", request.Strict);
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new AccountCurrenciesResponse(response);
        }

        /// <summary>
        /// The server_state command asks the server for various machine-readable information about the rippled server's current state.
        /// </summary>
        public async Task<ServerStateResponse> ServerState(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "server_state");
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new ServerStateResponse(response);
        }

        /// <summary>
        /// The account_lines method returns information about an account's trust lines, including balances in all non-XRP currencies and assets. 
        /// All information retrieved is relative to a particular version of the ledger.
        /// </summary>
        public async Task<AccountLinesResponse> AccountLines(AccountLinesRequest request, CancellationToken cancellationToken = default)
        {
            // N.B It would be interesting to see if we could write this to use IAsyncEnumerable instead of collecting all the data up front, but it's not
            // clear what that API would look like? Should Lines on AccountLinesResponse be an AsyncEnumerable? Should this return a 
            // Task<Tuple<AccountLinesResponse>, AsyncEnumerable<TrustLine>> where the response object just has account and ledger on it. Or even have this just return
            // AsyncEnumerable<TrustLine> and copy the account and ledger info onto each TrustLine object instead of just having it once?
            System.Text.Json.JsonElement? currentMarker = null;
            AccountLinesResponse accountLinesResponse = null;

            do
            {
                jsonBuffer.Clear();
                var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
                var thisId = ++currentId;
                using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("id", thisId);
                    writer.WriteString("command", "account_lines");
                    LedgerSpecification.Write(writer, request.Ledger);
                    writer.WriteString("account", request.Account.ToString());
                    if (request.Peer.HasValue)
                    {
                        writer.WriteString("peer", request.Peer.Value.ToString());
                    }
                    if (currentMarker.HasValue)
                    {
                        writer.WritePropertyName("marker");
                        currentMarker.Value.WriteTo(writer);
                    }
                    writer.WriteEndObject();
                }

                await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                var response = await ReceiveAsync(thisId, cancellationToken);
                if (accountLinesResponse == null)
                {
                    accountLinesResponse = new AccountLinesResponse(response);
                }
                else
                {
                    accountLinesResponse.Add(response);
                }

                if(response.TryGetProperty("marker", out var marker))
                {
                    // Have to clone because we clear the json buffer on each loop.
                    currentMarker = marker.Clone();
                }
                else
                {
                    currentMarker = null;
                }

            } while (currentMarker.HasValue);
            return accountLinesResponse;
        }
    }
}
