using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    public sealed class WebSocketApi : Api
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
            while (!cancellationTokenSource.IsCancellationRequested)
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
                                var request = json.RootElement.GetProperty("request").Clone();
                                RippleException exception;
                                if (json.RootElement.TryGetProperty("error_exception", out var element))
                                {
                                    exception = new RippleSubmitRequestException(error, element.GetString(), request);
                                }
                                else
                                {
                                    exception = new RippleRequestException(error, request);
                                }

                                lock (responses)
                                {
                                    if (responses.TryGetValue(id, out var task))
                                    {
                                        responses.Remove(id);
                                        task.SetException(exception);
                                    }
                                }
                            }
                            else
                            {
                                lock (responses)
                                {
                                    if (responses.TryGetValue(id, out var task))
                                    {
                                        responses.Remove(id);
                                        task.SetException(new NotSupportedException(string.Format("{0} not a supported status", status)));
                                    }
                                }
                            }
                        }

                        response.Clear();
                    }
                }
                catch (TaskCanceledException taskCanceledException)
                {
                    if (taskCanceledException.CancellationToken == cancellationTokenSource.Token)
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

        public override ValueTask DisposeAsync()
        {
            cancellationTokenSource.Cancel();
            return new ValueTask(receiveTask);
        }

        private async Task<System.Text.Json.JsonElement> ReceiveAsync(uint id, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<System.Text.Json.JsonElement>();
            lock (responses)
            {
                responses.Add(id, tcs);
            }
            return await tcs.Task;
        }

        public override async Task Ping(CancellationToken cancellationToken = default)
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

        public override async Task<Hash256> Random(CancellationToken cancellationToken = default)
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

        public override async Task<LedgerResponse> Ledger(LedgerRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "ledger");
                writer.WriteBoolean("binary", true);
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

        public override async Task<LedgerClosedResponse> LedgerClosed(CancellationToken cancellationToken = default)
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

        public override async Task<uint> LedgerCurrent(CancellationToken cancellationToken = default)
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

        public override async Task<FeeResponse> Fee(CancellationToken cancellationToken = default)
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

        public override async Task<AccountInfoResponse> AccountInfo(AccountInfoRequest request, CancellationToken cancellationToken = default)
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
                writer.WriteBoolean("queue", request.Queue);
                writer.WriteBoolean("signer_lists", request.SignerLists);
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new AccountInfoResponse(response);
        }

        public override async Task<AccountCurrenciesResponse> AccountCurrencies(AccountCurrenciesRequest request, CancellationToken cancellationToken = default)
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
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new AccountCurrenciesResponse(response);
        }

        public override async Task<ServerStateResponse> ServerState(CancellationToken cancellationToken = default)
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

        public override async Task<AccountLinesResponse> AccountLines(AccountLinesRequest request, CancellationToken cancellationToken = default)
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
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new AccountLinesResponse(response, async (marker, cancellationToken) =>
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
                    writer.WritePropertyName("marker");
                    marker.WriteTo(writer);
                    writer.WriteEndObject();
                }

                await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
                return await ReceiveAsync(thisId, cancellationToken);
            });
        }

        public override async Task<SubmitResponse> Submit(SubmitRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "submit");
                writer.WriteBase16String("tx_blob", request.TxBlob.Span);
                writer.WriteBoolean("fail_hard", request.FailHard);
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new SubmitResponse(response);
        }

        public override async Task<TransactionResponse> Tx(TxRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "tx");
                writer.WriteString("transaction", request.Transaction.ToString());
                if (request.MinLedger.HasValue) { writer.WriteNumber("min_ledger", request.MinLedger.Value); }
                if (request.MaxLedger.HasValue) { writer.WriteNumber("max_ledger", request.MaxLedger.Value); }
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new TransactionResponse(response);
        }

        public override async Task<TransactionResponse> TransactionEntry(TransactionEntryRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "tx");
                writer.WriteString("tx_hash", request.TxHash.ToString());
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new TransactionResponse(response);
        }

        public override async Task<NoRippleCheckResponse> NoRippleCheck(NoRippleCheckRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "noripple_check");
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account.ToString());
                writer.WriteString("role", request.Role);
                writer.WriteBoolean("transactions", request.Transactions);
                if (request.Limit.HasValue) { writer.WriteNumber("limit", request.Limit.Value); }
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new NoRippleCheckResponse(response);
        }

        public override async Task<WalletProposeResponse> WalletPropose(WalletProposeRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "wallet_propose");
                if (request.KeyType.HasValue)
                {
                    var type = request.KeyType.Value == KeyType.Secp256k1 ? "secp256k1" : "ed25519";
                    writer.WriteString("key_type", type);
                }
                if (request.Passphrase != null) { writer.WriteString("passphrase", request.Passphrase); }
                if (request.Seed != null) { writer.WriteString("seed", request.Seed); }
                if (request.SeedHex != null) { writer.WriteString("seed_hex", request.SeedHex); }
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new WalletProposeResponse(response);
        }

        public override async Task<GatewayBalancesResponse> GatewayBalances(GatewayBalancesRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            var thisId = ++currentId;
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteNumber("id", thisId);
                writer.WriteString("command", "gateway_balances");
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account.ToString());
                if (request.HotWallet != null)
                {
                    if (request.HotWallet.Length == 1)
                    {
                        writer.WriteString("hotwallet", request.HotWallet[0].ToString());
                    }
                    else
                    {
                        writer.WritePropertyName("hotwallet");
                        writer.WriteStartArray();
                        foreach (var account in request.HotWallet)
                        {
                            writer.WriteStringValue(account.ToString());
                        }
                        writer.WriteEndArray();
                    }
                }
                writer.WriteEndObject();
            }

            await socket.SendAsync(jsonBuffer.WrittenMemory, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var response = await ReceiveAsync(thisId, cancellationToken);
            return new GatewayBalancesResponse(response);
        }
    }
}
