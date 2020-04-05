using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    public sealed class JsonRpcApi : Api
    {
        private readonly HttpClient client;
        private readonly System.Buffers.ArrayBufferWriter<byte> jsonBuffer = new System.Buffers.ArrayBufferWriter<byte>();

        public JsonRpcApi(HttpClient httpClient)
        {
            client = httpClient;
        }

        private async Task<System.Text.Json.JsonElement> ReceiveAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var json = System.Text.Json.JsonDocument.Parse(body);
                var result = json.RootElement.GetProperty("result");

                var status = result.GetProperty("status").GetString();
                if (status == "success")
                {
                    return result;
                }
                else if (status == "error")
                {
                    var error = result.GetProperty("error").GetString();
                    var request = result.GetProperty("request");
                    RippleException exception;
                    if (result.TryGetProperty("error_exception", out var element))
                    {
                        exception = new RippleSubmitRequestException(error, element.GetString(), request);
                    }
                    else
                    {
                        exception = new RippleRequestException(error, request);
                    }

                    throw exception;
                }
                else
                {
                    throw new NotSupportedException(string.Format("{0} not a supported status", status));
                }
            }
            else
            {
                throw new RippleException(body);
            }
        }

        public override ValueTask DisposeAsync()
        {
            client.Dispose();
            return new ValueTask();
        }

        public override async Task<AccountCurrenciesResponse> AccountCurrencies(AccountCurrenciesRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "account_currencies");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account.ToString());
                writer.WriteBoolean("strict", request.Strict);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new AccountCurrenciesResponse(response);
        }

        public override async Task<AccountInfoResponse> AccountInfo(AccountInfoRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "account_info");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account.ToString());
                writer.WriteBoolean("strict", request.Strict);
                writer.WriteBoolean("queue", request.Queue);
                writer.WriteBoolean("signer_lists", request.SignerLists);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new AccountInfoResponse(response);
        }

        public override async Task<AccountLinesResponse> AccountLines(AccountLinesRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "account_lines");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteString("account", request.Account.ToString());
                if (request.Peer.HasValue)
                {
                    writer.WriteString("peer", request.Peer.Value.ToString());
                }
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new AccountLinesResponse(response, async (marker, cancellationToken) =>
            {
                jsonBuffer.Clear();
                var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
                using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
                {
                    writer.WriteStartObject();
                    writer.WriteString("method", "account_lines");
                    writer.WritePropertyName("params");
                    writer.WriteStartArray();
                    writer.WriteStartObject();
                    LedgerSpecification.Write(writer, request.Ledger);
                    writer.WriteString("account", request.Account.ToString());
                    if (request.Peer.HasValue)
                    {
                        writer.WriteString("peer", request.Peer.Value.ToString());
                    }
                    writer.WritePropertyName("marker");
                    marker.WriteTo(writer);
                    writer.WriteEndObject();
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
                return await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            });
        }
        public override async Task<FeeResponse> Fee(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "fee");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new FeeResponse(response);
        }

        public override async Task<LedgerResponse> Ledger(LedgerRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "ledger");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteBoolean("binary", true);
                writer.WriteBoolean("full", request.Full);
                writer.WriteBoolean("accounts", request.Accounts);
                writer.WriteBoolean("transactions", request.Transactions);
                writer.WriteBoolean("expand", request.Expand);
                writer.WriteBoolean("owner_funds", request.OwnerFunds);
                writer.WriteBoolean("queue", request.Queue);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new LedgerResponse(response);
        }

        public override async Task<LedgerClosedResponse> LedgerClosed(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "ledger_closed");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new LedgerClosedResponse(response);
        }

        public override async Task<uint> LedgerCurrent(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "ledger_current");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return response.GetProperty("ledger_current_index").GetUInt32();
        }

        public override async Task Ping(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "ping");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var _ = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
        }

        public override async Task<Hash256> Random(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "random");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new Hash256(response.GetProperty("random").GetString());
        }

        public override async Task<ServerStateResponse> ServerState(CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "server_state");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new ServerStateResponse(response);
        }

        public override async Task<SubmitResponse> Submit(SubmitRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "submit");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteBase16String("tx_blob", request.TxBlob);
                writer.WriteBoolean("fail_hard", request.FailHard);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return new SubmitResponse(response);
        }

        public override async Task<TransactionResponse> Tx(TxRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "tx");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("transaction", request.Transaction.ToString());
                if (request.MinLedger.HasValue) { writer.WriteNumber("min_ledger", request.MinLedger.Value); }
                if (request.MaxLedger.HasValue) { writer.WriteNumber("max_ledger", request.MaxLedger.Value); }
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return TransactionResponse.ReadJson(response);
        }

        public override async Task<TransactionResponse> TransactionEntry(TransactionEntryRequest request, CancellationToken cancellationToken = default)
        {
            jsonBuffer.Clear();
            var options = new System.Text.Json.JsonWriterOptions() { SkipValidation = true };
            using (var writer = new System.Text.Json.Utf8JsonWriter(jsonBuffer, options))
            {
                writer.WriteStartObject();
                writer.WriteString("method", "transaction_entry");
                writer.WritePropertyName("params");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("tx_hash", request.TxHash.ToString());
                LedgerSpecification.Write(writer, request.Ledger);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            var content = new ReadOnlyMemoryContent(jsonBuffer.WrittenMemory);
            var response = await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
            return TransactionResponse.ReadJson(response);
        }
    }
}
