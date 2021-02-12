using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ibasa.Ripple
{
    public sealed class JsonRpcApi : Api
    {
        private readonly HttpClient client;

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

        protected override uint WriteHeader(System.Text.Json.Utf8JsonWriter writer, string command)
        {
            writer.WriteString("method", command);
            writer.WritePropertyName("params");
            writer.WriteStartArray();
            writer.WriteStartObject();
            return 0;
        }

        protected override void WriteFooter(System.Text.Json.Utf8JsonWriter writer)
        {
            writer.WriteEndObject();
            writer.WriteEndArray();
        }
        protected override async Task<System.Text.Json.JsonElement> SendReceiveAsync(uint requestId, ReadOnlyMemory<byte> json, CancellationToken cancellationToken)
        {
            var content = new ReadOnlyMemoryContent(json);
            return await ReceiveAsync(await client.PostAsync("/", content, cancellationToken));
        }

        public async override ValueTask DisposeAsync()
        {
            client.Dispose();
            await base.DisposeAsync();
        }
    }
}
