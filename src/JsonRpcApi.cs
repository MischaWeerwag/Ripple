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
                    throw new RippleRequestException(error, result.GetProperty("request"));
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

        public override async Task<AccountCurrenciesResponse> AccountCurrencies(AccountCurrenciesRequest request = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task<AccountInfoResponse> AccountInfo(AccountInfoRequest request = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task<AccountLinesResponse> AccountLines(AccountLinesRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        public override async Task<FeeResponse> Fee(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task<LedgerResponse> Ledger(LedgerRequest request = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task<LedgerClosedResponse> LedgerClosed(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task<uint> LedgerCurrent(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
